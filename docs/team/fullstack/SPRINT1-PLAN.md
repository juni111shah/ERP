# Sprint 1 plan: identity & access, workspaces, entity scope, modules (Sprint 2 overflow included)

Revised 22 Oct 2026 after coordinator decisions (§10). Sprint 1 is sized for **one developer, ~10 days**; everything else is in
**§8 Sprint 2**. Assumes the sprint-0 scaffold (`/workspace/erp`) is merged: Identity + JWT + refresh rotation, `IOrgContext`,
global filters, `IModule`/`ModuleRegistry`/`ModuleEnabledFilter`, `OrganizationModule`, `OrganizationMember`, `Role/RolePermission/
UserRole/UserLegalEntityAccess`, `PasswordResetToken`, Angular shell. Spec refs: §2.2–2.3, §4.26, §4.30, §7, §10.3–10.4, §12.
Conventions: `CONVENTIONS.md`. Reusable code: `starter-code/`. Estimates are ideal days incl. unit/integration tests; spec §13
recommends a 20–30 % buffer on top (see risks).

---

## 1. Permissions and role templates

- Keys `<module>.<resource>.<action>`, resource **plural**, lower case: `core.users.read|write`, `core.roles.read|write`,
  `core.modules.manage`, `core.entities.read|write`, `core.settings.read|write`, `core.customfields.read|manage`, `core.audit.read`,
  `core.approvals.decide`, `core.sensitive.read`, `realestate.unittypes.read|write`, `finance.cheques.approve`… Declared in
  `IModule.Permissions`; `ModuleRegistry` rejects keys not prefixed by the module key. `*` = Org admin (all registered keys).
- **Role templates** (code, per core/industry: `RoleTemplate(Key, Name, Description, ModuleKey, Permissions[])`) — the nine spec roles
  (§2.3) with the §7.2 matrix mapped to module keys (e.g. spec `portfolio` RW → `realestate.properties.*`, `realestate.units.*`,
  `realestate.unittypes.*`, `core.entities.*`; spec `admin.users` → `core.users.*` + `core.roles.read`).
- **Copy once**: at onboarding (core + chosen industry) and when a module is enabled, each template is copied into the org as a `Role`
  (`IsBuiltIn = true`, `ModuleKey`, `TemplateKey`) with its `RolePermission` rows — idempotent by `(OrgId, TemplateKey)`. Later
  template changes in code never rewrite existing org roles (new keys ship via an explicit "add permission to roles" migration step).
- Sprint 1: built-in roles are read-only; assignment only. Custom roles (create/clone/edit/delete) → Sprint 2.
- Effective permissions = ∪(membership roles' keys) ∩ keys of **enabled** modules (+ core).
- JWT (workspace-scoped, spec §10.4): `sub` (global user id), `org`, `mid` (membership id), `perms`, `ents` (`*` or ids), `ver`.
  `ver` = per-membership counter bumped on role/entity-access/active/module changes; middleware compares with a 60 s cache and
  returns 401 `code: permissions_changed` → client silent refresh.

## 2. Identity & workspaces (global identity, workspace picker)

Model: **one global identity per email** (`AppUser`, email unique globally, password + lockout global). Workspace access =
`OrganizationMember` (OrgId, UserId, Status Invited/Active/Disabled, LastSignInAt) + `UserRole` + `UserLegalEntityAccess` per membership.

Auth flow
| Endpoint | Request → Response | Rules |
|---|---|---|
| `POST /api/v1/auth/login` | `{ email, password, workspace? }` (workspace = sign-in key / subdomain, spec §10.3) | Wrong credentials / locked → 401 neutral "Invalid email or password". Correct password: 0 active memberships → 403 `no_workspace`; exactly 1 (or `workspace` matches one) → tokens; >1 → `200 { requiresWorkspace: true, selectionToken, workspaces: [{ orgId, name, signInKey, lastSignInAt }] }` |
| `POST /api/v1/auth/select-workspace` | `{ selectionToken, orgId }` → access token + refresh cookie | `selectionToken`: opaque, hashed, single-use, 5 min, bound to user + IP-family; org must be an active membership |
| `POST /api/v1/auth/switch-workspace` | (authenticated) `{ orgId }` → new tokens | revokes current refresh family; header avatar menu "Switch workspace" |
| `POST /api/v1/auth/refresh` · `/logout` · `/change-password` | scaffold | refresh token carries the org; change-password is global |
| `GET /api/v1/me` | → `MeDto` | user, current workspace, workspaces list, permissions, `ver`, allowed entities, preferred entity, enabled modules, menu |

Self-service password reset (decision)
| Endpoint | Behaviour |
|---|---|
| `POST /api/v1/auth/forgot` `{ email }` | Always `202 { message: "If an account exists for that email, we've sent a link. Check your email." }` (same body/timing whether or not the email exists; constant-time path + queued email). Creates a 32-byte random token, stores **SHA-256 hash** in `PasswordResetToken` (UserId, ExpiresAt = now + **30 min**, UsedAt, RevokedAt); **issuing a new link revokes all older unused tokens** of that user. Rate limit 3/h per email and 10/h per IP. |
| `POST /api/v1/auth/reset` `{ token, newPassword }` | Token must exist, be unused, unrevoked, unexpired → else 400 neutral "This link is invalid or has expired". On success (one transaction): set password (policy validated → 400 `errors.newPassword`), mark token used (**single-use**), revoke other tokens, **clear lockout** (`AccessFailedCount = 0`, `LockoutEnd = null`), revoke all refresh tokens of the identity, audit `PasswordReset`, send "password changed" email. |
| Admin `POST /api/v1/users/{id}/reset-password` (core.users.write) | Sends the same self-service reset link to the user (no temporary password shown) — because the identity is global, an admin of one workspace must not set a password that unlocks other workspaces. |

## 3. Users & roles API and screens

| Method & path | Permission | Request → Response | Notes |
|---|---|---|---|
| `GET /api/v1/users?page&pageSize&sort&q&filter=status:eq:active&filter=roleId:eq:…` | core.users.read | → `PagedResult<UserListItemDto>` | members of the current workspace |
| `GET /api/v1/users/{id}` | core.users.read | → `UserDto` + ETag | `id` = membership id |
| `POST /api/v1/users` | core.users.write | `AddUserDto` → 201 `UserDto` | existing global email → adds membership (status Invited, "you were added to {workspace}" email, no password change); new email → creates identity + membership and emails a set-password link (same token mechanism, 7-day expiry for invites) |
| `PUT /api/v1/users/{id}` | core.users.write | `UpdateUserDto` + If-Match → `UserDto` | edits membership only (roles, entity access, active); `fullName` editable only by the user (`/me`) — shown read-only to admins (global identity) |
| `POST /api/v1/users/{id}/reset-password` | core.users.write | → 202 | sends reset link (§2) |
| `GET /api/v1/roles` | core.roles.read or core.users.read | → `RoleDto[]` | built-ins from templates |
| `GET /api/v1/permissions` | core.roles.read | → `PermissionCatalogDto` | enabled modules only (used read-only in sprint 1) |

DTOs
```ts
UserListItemDto { id; userId; fullName; email; isCurrentUser; status: 'invited'|'active'|'disabled'; roles: {id,name}[];
                  entityAccess: 'all'|'selected'; legalEntities: {id,code,name}[]; lastSignInAt?: string; }
UserDto         extends UserListItemDto { createdAt; rowVersion; }
AddUserDto      { email; fullName (used only for new identities); roleIds: string[] (default Viewer); entityAccess: 'all'|'selected'; legalEntityIds: string[] }
UpdateUserDto   { roleIds; entityAccess; legalEntityIds; isActive }
RoleDto         { id; name; description?; isBuiltIn; moduleKey?; templateKey?; permissions: string[]; userCount }
PermissionCatalogDto { modules: { key; name; resources: { key; label; actions: { key; action; description }[] }[] }[] }
MeDto { user:{id,fullName,email}; workspace:{orgId,name,signInKey}; workspaces:{orgId,name}[]; permissions: string[]; permissionVersion: number;
        legalEntities:{id,code,name}[]; allEntities: boolean; preferredLegalEntityId?: string; modules:{key,name,uiRoute}[]; menu: MenuGroupDto[] }
```
Rules: `roleIds` ≥ 1; `selected` ⇒ ≥ 1 entity of this org; cannot disable yourself; cannot remove/disable the **last** active Org admin
(409 `last_admin`); membership changes bump `ver`; disabling a membership revokes that workspace's refresh tokens only; all writes audited.

Screens (`/users`, spec §4.26)
- Users tab: table Name (+ "You", email below), Roles tags, Legal entities, Last sign-in, Status (Invited/Active/Disabled), Edit, Send reset link; toolbar search, Role/Status filters, `+ Add user`.
- Add/Edit modal: Email (add; locked on edit), Full name (add, new identities only), Roles multi-select (+ selected role description), Legal entity access radio (All / Only selected → entity multi-select), Active (edit). Server errors mapped to fields.
- Roles tab (sprint 1 read-only): Role, Can, Permissions (key tags, horizontal scroll), Users count.
- Auth pages: Login → **workspace picker** (list of workspaces after correct password, remembers last), Forgot password (neutral confirmation), Reset password (token from link), Switch workspace in avatar menu.

## 4. Legal-entity switcher (spec §4.30)

- Backend: `EntityScopeMiddleware` validates `X-Entity-Id` ∈ membership's allowed + active entities (403 `entity_not_allowed`), sets `IOrgContext.SetLegalEntity`; global filter narrows; entity-scoped creates in "All" mode require `legalEntityId` in the body (validated ∈ allowed); `PUT /api/v1/me/preferences { defaultLegalEntityId }` per membership.
- Frontend: header `EntitySwitcherComponent` (nz-select: "All legal entities" when > 1 allowed + entities; static label for one), `SessionStore.currentEntityId`, `entityScopeInterceptor`, persistence (server + localStorage, validated), on change: invalidate caches and reload current data; record outside new scope → back to list.

## 5. Modules per organization

- Composition: installed modules = `IModule` implementations in `ModuleRegistry`; enablement per organization in `OrganizationModule`. **`core` is always on** (cannot be disabled; not stored). **Industry is chosen at onboarding**: the industry template enables its modules and copies their role templates/settings (one transaction, idempotent).
- `GET /api/v1/modules` (core.modules.manage or core.settings.read) — installed + planned: key, name, description, category, isCore, requires[], status, isEnabled, enabledAt.
- `POST /api/v1/modules/{key}/enable` (core.modules.manage) — installed + all `Requires` enabled (409 `missing_dependency` + list); upsert `OrganizationModule`; copy-once role templates + settings + number sequences; bump `ver` of all memberships; invalidate `IModuleAccess` cache.
- `POST /api/v1/modules/{key}/disable` — `core`/isCore → 409 `core_module`; enabled dependents → 409 `dependents_enabled`; set `IsEnabled=false`; **data kept**; bump `ver`; invalidate caches.
- **Gate (403)**: every module endpoint lives under `/api/v1/m/{key}/…` on the group the host creates per module with `ModuleEnabledFilter` (controllers: `[RequireModule(key)]`, see starter kit) → `403 { type: "https://erp.local/problems/module-disabled", code: "module_disabled" }`. Customizable entity types and metadata of disabled modules are hidden / 403.
- **Menu & UI**: `/me.menu` = core + enabled modules' `MenuItemDefinition`s filtered by permissions, grouped by `GroupId/GroupLabel/GroupOrder` (spec §3.3). Sidebar renders only from `/me.menu`; module routes use `canMatch: [moduleEnabledGuard(key)]`; `moduleDisabledInterceptor` handles a module disabled mid-session (toast + home).
- Screen: Settings → Modules (cards by category, status tag, Enable/Disable with dependency + "data is kept" confirmation).

## 6. Sprint 1 task breakdown (1 developer)

| # | Task | Est. (d) | Depends on |
|---|---|---|---|
| S1-1 | Merge kit basics into scaffold: ProblemDetails handler, list contract, `IAppDbContext.Entry`, JSON enum converter, `[RequireModule]` for controllers | 0.75 | scaffold merged |
| S1-2 | Permission catalog endpoint + role templates (9 spec roles mapped) + copy-once seeding | 1.0 | S1-1, BA mapping |
| S1-3 | Users API (list/get/add-member/update, last-admin, entity access, invite email) | 1.25 | S1-2 |
| S1-4 | Global-identity login + workspace picker (`select-workspace`, `switch-workspace`), claims `perms/ents/ver/mid`, `ver` middleware, `GET /me` | 1.25 | S1-3 |
| S1-5 | `/auth/forgot` + `/auth/reset` + admin "send reset link" (tokens, expiry, revoke older, lockout clear, rate limit) | 0.75 | S1-4 |
| S1-6 | Entity scope middleware + write-side checks + `/me/preferences` | 0.75 | S1-4 |
| S1-7 | Modules API (list/enable/disable, deps, copy-once seeding, cache) + 403 gate tests + `/me.menu` | 1.0 | S1-2 |
| S1-8 | FE: session bootstrap (`provideAppInitializer` → `/me`), `*hasPermission`, `permissionGuard`, `moduleEnabledGuard`, interceptors; login + workspace picker + forgot/reset pages | 1.0 | S1-4, S1-5 |
| S1-9 | FE: entity switcher | 0.5 | S1-8, S1-6 |
| S1-10 | FE: Users tab + add/edit modal + send reset link; Roles tab read-only | 1.0 | S1-8, S1-3 |
| S1-11 | FE: Settings → Modules + dynamic sidebar | 0.75 | S1-8, S1-7 |
| | **Sprint 1 total** | **10.0 d** | |

Critical path: S1-1 → S1-2 → S1-3 → S1-4 → (S1-5, S1-6) → S1-8 → S1-9/S1-10. No buffer inside 10 d: if the sprint slips, S1-11 (Modules screen; API stays) is the first item to move.

## 7. Sprint 1 acceptance criteria

Identity & auth
- AC-A1: A person with one email in two workspaces signs in with one password; after the correct password the **workspace picker** lists both; choosing one issues a token scoped to it; "Switch workspace" changes scope without re-entering the password.
- AC-A2: Wrong password / unknown email / locked account all return the same 401 message; one-workspace users skip the picker; sign-in key/subdomain preselects the workspace.
- AC-A3: `/auth/forgot` returns the same neutral "check your email" response for existing and unknown emails.
- AC-A4: Reset link works **once**, expires after **30 min**, and requesting a new link invalidates the previous one (old link → "invalid or expired").
- AC-A5: Successful reset clears lockout (a locked user can sign in immediately with the new password) and revokes all refresh tokens.
Users & roles
- AC-U1: Admin adds an existing email → membership created (no password change, notification email); adds a new email → identity + set-password link.
- AC-U2: "Only selected entities" users see only those entities' rows; another entity's `X-Entity-Id` → 403 `entity_not_allowed`.
- AC-U3: Role/entity-access changes take effect ≤ 60 s without re-login (`permissions_changed` → silent refresh).
- AC-U4: Disabling a membership blocks that workspace only; the same identity keeps access to its other workspaces.
- AC-U5: Last Org admin cannot be removed/disabled; self-disable refused (409, message on form).
- AC-U6: Built-in roles exist in every new org from templates; changing a template in code does not modify existing org roles.
- AC-U7: Users without `core.users.write` see no Add/Edit/Reset buttons and get 403 from the API.
Entity switcher
- AC-E1: Switcher lists "All legal entities" (if > 1 allowed) + allowed entities; choice survives reload and device change.
- AC-E2: Switching re-scopes the current list immediately; entity-scoped create in "All" mode requires choosing an allowed entity.
- AC-E3: Org A user cannot read/update org B records by id (404) — isolation test per aggregate.
Modules
- AC-M1: Onboarding with industry "Real estate" enables `realestate` and copies its role templates; `core` cannot be disabled (409).
- AC-M2: Disabling `realestate` → its menu group disappears after the next `/me`, its routes redirect home, `GET /api/v1/m/realestate/unit-types` returns **403 module-disabled**, its data is untouched and returns when re-enabled.
- AC-M3: Enabling with a disabled dependency → 409 listing missing modules; enabling twice creates no duplicate roles/settings.
- AC-M4: Permission keys of a disabled module are not in the token (`perms`) after refresh.

## 8. Sprint 2 (overflow + deferred)

| # | Task | Est. (d) |
|---|---|---|
| S2-1 | Custom roles API: create / clone-from-template / edit / delete (409 `role_in_use`, `builtin_role`), `ver` bump of members | 1.0 |
| S2-2 | Custom-role editor UI (permission matrix from `/permissions`, clone, delete) | 1.5 |
| S2-3 | Custom fields backend: reconcile `CustomFieldDefinition` + migration, cached provider, validation behaviour, definitions CRUD (`/api/v1/custom-fields/{entityType}`), metadata endpoint | 2.5 |
| S2-4 | Custom-field list filter/sort (JSON_VALUE) + SQL integration tests | 1.0 |
| S2-5 | FE CRUD kit in shell + dynamic fields/filters + Studio page | 2.5 |
| S2-6 | **Performance tests**: list with custom filters (10k rows, p95 < 500 ms), token size with max permissions, `/me` latency, computed-column promotion check | 1.0 |
| S2-7 | Playwright E2E: login → workspace picker → switch entity → add user → add custom field → visible in form/list/filter | 1.5 |
| | **Total** | **11.0 d** (> 10 d: S2-7 can start in sprint 2 and finish in sprint 3, or trim S2-6 to the custom-filter test) |

Spec §13 sprint 2 ("Master data I": legal entities, properties, units, customers, vendors, numbering, documents/history tabs) starts once
S2-3/S2-5 land — re-plan sprint 2 vs 3 with the coordinator.

Sprint 2 acceptance criteria (carried)
- AC-R1: A custom role cloned from a template can be edited and assigned; built-ins can't be edited/deleted; unknown key → 400 `permissions[i]`.
- AC-C1: **Add a field via API** (`POST /api/v1/custom-fields/realestate.unit-type` `{ "fieldKey": "cf_floor_count", "dataType": "number", "label": "Floor count", "showInList": true, "isFilterable": true, "isSortable": true }`) **or via the Studio UI** → without redeploy or restart the Unit types form shows it in its section/order, the list shows a sortable column, and the toolbar shows a server-side range filter.
- AC-C2: Required/min/max/regex/options enforced server-side; 400 `errors["customFields.cf_…"]` shown under the control.
- AC-C3: Unknown keys → 400; defaults on create; deactivated fields hidden but values preserved; key/type immutable (409); deleted keys not reusable.
- AC-C4: Metadata reflects changes on the next request (cache invalidated), 304 when unchanged; entity-scoped definitions follow the header entity; disabled module's types → 403.
- AC-P1: Performance targets of S2-6 met or a promotion script provided.

## 9. Dependencies & risks

Dependencies: scaffold merged (auth, tenancy, `IModule`, `OrganizationMember`, `PasswordResetToken`); BA mapping of §7.2 → keys; UI/UX designs for users modal, workspace picker, modules screen; email sender (SMTP/SendGrid) for invites and reset links; SQL Server 2017+.

| Risk | Mitigation |
|---|---|
| 10.0 d with no buffer (spec advises +20–30 %) | S1-11 first to move; daily burn-down; keep S1-1 to the minimum kit merge |
| Global identity: admin of workspace A affecting access to B | admins edit memberships only; password only via user's own reset link; global lockout documented |
| Account enumeration via login/forgot/add-user | neutral messages, constant-time paths, rate limits; add-user reveals existence only to admins (acceptable, audited) |
| Reset-token theft / replay | hashed tokens, 30 min, single-use, newest-only, HTTPS links, revoke refresh tokens on reset |
| Large JWT with many permissions | `permsRef` fallback + server cache keyed by `ver` |
| Stale permissions after role/module change | `ver` middleware (60 s) |
| Module disable leaves stale menus/caches | `ver` bump + cache invalidation + `moduleDisabledInterceptor` |
| Template evolution vs copied roles drift | explicit, reviewed "grant new key to roles" migrations; never silent rewrite |

## 10. Decisions recorded (coordinator, 22 Oct 2026)

- D1 Permission keys `<module>.<resource>.<action>`, resource plural (`finance.cheques.approve`, `core.customfields.manage`).
- D2 Module APIs under `/api/v1/m/{key}/…`; disabled module → 403 (module-disabled) and data kept; core flat under `/api/v1/`.
- D3 Sprint 1 uses role templates copied once; custom-role editor and performance tests in Sprint 2.
- D4 One global identity per email; an email can belong to several workspaces; workspace picker after a correct password.
- D5 Self-service `/auth/forgot`: single-use tokens, 30 min expiry, a new link invalidates older ones, successful reset clears lockout, neutral "check your email" response.
- D6 Modules enabled per organization via `IModule` + registry; industry picked at onboarding; core always on.
- D7 Frontend on Angular 19 + ng-zorro-antd 19.3.1.
- (earlier) Studio permission `core.customfields.manage` / `.read`; "All legal entities" shown when a user has > 1 allowed entity.

Still open
- O1 Unit type: free text on Unit (spec) vs lookup master data (kit example).
- O2 Invite expiry for new identities (proposed 7 days) and whether invited members count as seats.
