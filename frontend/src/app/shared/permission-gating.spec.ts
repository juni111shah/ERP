import { Component } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { By } from '@angular/platform-browser';
import { SessionStore, MeResponse } from '../core/session/session.store';
import { HasPermissionDirective } from './has-permission.directive';

@Component({
  standalone: true,
  imports: [HasPermissionDirective],
  template: `
    <div class="list-page">
      <button id="create-btn" *erpHasPermission="'pos.registers.manage'">Add New Register</button>
      <button id="edit-btn" *erpHasPermission="'pos.registers.manage'">Edit Register</button>
      <button id="view-btn" *erpHasPermission="'pos.registers.read'">View Register</button>
    </div>
  `
})
class MockModuleListPageComponent {}

describe('UI Permission Gating (TASK-013)', () => {
  let fixture: ComponentFixture<MockModuleListPageComponent>;
  let sessionStore: SessionStore;

  const createMockMe = (permissions: string[]): MeResponse => ({
    user: { id: 'u1', email: 'user@example.com', fullName: 'Test User', mustChangePassword: false },
    workspace: { id: 'w1', code: 'W1', name: 'Test Org', countryCode: 'AE', baseCurrency: 'AED', timeZone: 'UTC', defaultLocale: 'en' },
    workspaces: [],
    permissions,
    legalEntities: [],
    allLegalEntities: true,
    enabledModules: [{ key: 'pos', uiRoute: '/pos' }]
  });

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [MockModuleListPageComponent]
    }).compileComponents();

    sessionStore = TestBed.inject(SessionStore);
    fixture = TestBed.createComponent(MockModuleListPageComponent);
  });

  it('hides create and edit action buttons for read-only users', () => {
    sessionStore.me.set(createMockMe(['pos.registers.read']));
    TestBed.flushEffects();
    fixture.detectChanges();

    const createBtn = fixture.debugElement.query(By.css('#create-btn'));
    const editBtn = fixture.debugElement.query(By.css('#edit-btn'));
    const viewBtn = fixture.debugElement.query(By.css('#view-btn'));

    expect(createBtn).toBeNull();
    expect(editBtn).toBeNull();
    expect(viewBtn).not.toBeNull();
  });

  it('renders create and edit action buttons when user holds manage permission', () => {
    sessionStore.me.set(createMockMe(['pos.registers.read', 'pos.registers.manage']));
    TestBed.flushEffects();
    fixture.detectChanges();

    const createBtn = fixture.debugElement.query(By.css('#create-btn'));
    const editBtn = fixture.debugElement.query(By.css('#edit-btn'));
    const viewBtn = fixture.debugElement.query(By.css('#view-btn'));

    expect(createBtn).not.toBeNull();
    expect(editBtn).not.toBeNull();
    expect(viewBtn).not.toBeNull();
  });

  it('renders all action buttons when user has wildcard admin permission', () => {
    sessionStore.me.set(createMockMe(['*']));
    TestBed.flushEffects();
    fixture.detectChanges();

    const createBtn = fixture.debugElement.query(By.css('#create-btn'));
    const editBtn = fixture.debugElement.query(By.css('#edit-btn'));

    expect(createBtn).not.toBeNull();
    expect(editBtn).not.toBeNull();
  });
});
