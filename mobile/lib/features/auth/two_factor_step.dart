import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../core/api_client.dart';
import '../../core/session.dart';
import '../../theme/app_theme.dart';
import '../../theme/tokens.dart';

enum TwoFactorMode { setup, verify }

/// The QR image bytes of a `data:image/png;base64,...` URI, or null when it is not one.
Uint8List? qrBytes(String dataUri) {
  if (!dataUri.startsWith('data:image/')) return null;
  try {
    return UriData.parse(dataUri).contentAsBytes();
  } on FormatException {
    return null;
  }
}

/// The second sign-in step after a correct password.
/// - [TwoFactorMode.setup]: first sign-in without an authenticator. Shows the QR code of the otpauth:// link ("Can't scan? Show key"
///   reveals the key with a copy button), takes the first code, then shows the one-time recovery codes before the session starts.
/// - [TwoFactorMode.verify]: returning user. Takes the code from the authenticator app (or a recovery code).
/// The 6-digit field submits by itself on the sixth digit and offers the platform's one-time-code autofill.
class TwoFactorStep extends ConsumerStatefulWidget {
  const TwoFactorStep({super.key, required this.mode, required this.challengeToken, required this.onCancel});

  final TwoFactorMode mode;
  final String challengeToken;

  /// "Back to sign in", or the challenge expired: return to email and password.
  final VoidCallback onCancel;

  @override
  ConsumerState<TwoFactorStep> createState() => _TwoFactorStepState();
}

class _TwoFactorStepState extends ConsumerState<TwoFactorStep> {
  final _code = TextEditingController();
  final _focus = FocusNode();
  TwoFactorSetupInfo? _setup;
  bool _loadingSetup = false;
  bool _showKey = false;
  bool _useRecovery = false;
  bool _busy = false;
  bool _expired = false;
  String? _error;
  TwoFactorEnabled? _enabled;

  @override
  void initState() {
    super.initState();
    if (widget.mode == TwoFactorMode.setup) _loadSetup();
  }

  @override
  void dispose() {
    _code.dispose();
    _focus.dispose();
    super.dispose();
  }

  Future<void> _loadSetup() async {
    setState(() => _loadingSetup = true);
    try {
      final s = await ref.read(sessionProvider.notifier).beginTwoFactorSetup(widget.challengeToken);
      if (mounted) setState(() => _setup = s);
    } on ApiException catch (e) {
      _fail(e);
    } finally {
      if (mounted) setState(() => _loadingSetup = false);
    }
  }

  bool get _canSubmit {
    final v = _code.text.trim();
    return _useRecovery ? v.length >= 6 : RegExp(r'^\d{6}$').hasMatch(v);
  }

  void _onChanged(String value) {
    setState(() => _error = null);
    if (!_useRecovery && value.length == 6 && !_busy) _submit();
  }

  Future<void> _submit() async {
    if (_busy || !_canSubmit) return;
    setState(() {
      _busy = true;
      _error = null;
    });
    try {
      final outcome = await ref.read(sessionProvider.notifier).verifyTwoFactor(widget.challengeToken, _code.text);
      if (!mounted) return;
      switch (outcome) {
        case final TwoFactorEnabled enabled:
          setState(() => _enabled = enabled);
        case NeedsWorkspace(:final selectionToken, :final workspaces):
          ref.read(pendingWorkspaceProvider.notifier).set(PendingWorkspaceSelection(selectionToken, workspaces));
        default:
          break; // signed in: the router moves on once the session exists
      }
    } on ApiException catch (e) {
      _fail(e);
    } finally {
      if (mounted) setState(() => _busy = false);
    }
  }

  void _fail(ApiException e) {
    if (!mounted) return;
    _code.clear();
    setState(() {
      if (e.status == 401 && RegExp('expired|sign in again', caseSensitive: false).hasMatch(e.message)) {
        _expired = true;
      } else {
        _error = switch (e.status) {
          401 => "That code didn't work. Check the time on your phone and enter the newest code.",
          423 => 'Too many wrong codes. Your account is locked for a few minutes.',
          429 => 'Too many attempts. Please wait a minute and try again.',
          0 => 'Cannot reach the server. Check your connection.',
          _ => e.message,
        };
      }
    });
    _focus.requestFocus();
  }

  void _finish() {
    final enabled = _enabled!;
    if (enabled.workspaceChoice case final w?) {
      ref.read(pendingWorkspaceProvider.notifier).set(PendingWorkspaceSelection(w.selectionToken, w.workspaces));
    } else if (enabled.session case final s?) {
      ref.read(sessionProvider.notifier).complete(s);
    }
  }

  Future<void> _copy(String text, String message) async {
    await Clipboard.setData(ClipboardData(text: text));
    if (mounted) ScaffoldMessenger.maybeOf(context)?.showSnackBar(SnackBar(content: Text(message)));
  }

  @override
  Widget build(BuildContext context) {
    if (_expired) return _expiredView(context);
    if (_enabled != null) return _recoveryView(context, _enabled!);
    return _codeView(context);
  }

  Widget _header(BuildContext context, IconData icon, String title, String subtitle) {
    final theme = Theme.of(context);
    final erp = ErpTheme.of(context);
    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        Container(
          width: 48,
          height: 48,
          decoration: BoxDecoration(color: erp.brandSubtle, borderRadius: BorderRadius.circular(ErpRadius.card)),
          child: Icon(icon, color: erp.brand),
        ),
        const SizedBox(height: ErpSpace.s4),
        Text(title, style: theme.textTheme.headlineSmall?.copyWith(fontWeight: FontWeight.w700, letterSpacing: -0.3)),
        const SizedBox(height: ErpSpace.s1),
        Text(subtitle, style: theme.textTheme.bodyMedium?.copyWith(color: erp.muted)),
        const SizedBox(height: ErpSpace.s5),
      ],
    );
  }

  Widget _codeView(BuildContext context) {
    final theme = Theme.of(context);
    final erp = ErpTheme.of(context);
    final setup = widget.mode == TwoFactorMode.setup;
    final s = _setup;
    final bytes = s == null ? null : qrBytes(s.qrCodeDataUri);
    return Column(
      crossAxisAlignment: CrossAxisAlignment.stretch,
      children: [
        if (setup)
          _header(context, Icons.shield_outlined, 'Set up two-factor sign-in',
              'Your account needs an authenticator app (such as Google Authenticator) to sign in. Scan the QR code with the app, then enter the 6-digit code it shows.')
        else
          _header(context, Icons.shield_outlined, 'Enter your verification code', 'Open your authenticator app and enter the 6-digit code for this account.'),
        if (setup) ...[
          Container(
            padding: const EdgeInsets.all(ErpSpace.s4),
            decoration: BoxDecoration(
              color: erp.surface,
              borderRadius: BorderRadius.circular(ErpRadius.panel),
              border: Border.all(color: erp.border),
            ),
            child: Column(
              children: [
                Container(
                  // The QR stays dark-on-white in dark mode too, so every authenticator camera can read it.
                  padding: const EdgeInsets.all(ErpSpace.s2),
                  decoration: BoxDecoration(color: Colors.white, borderRadius: BorderRadius.circular(ErpRadius.control)),
                  child: SizedBox.square(
                    dimension: 184,
                    child: bytes != null
                        ? Image.memory(bytes, key: const Key('twofa-qr'), filterQuality: FilterQuality.none, semanticLabel: 'QR code for your authenticator app')
                        : Center(child: _loadingSetup ? const CircularProgressIndicator() : Icon(Icons.qr_code_2, size: 64, color: erp.muted)),
                  ),
                ),
                if (s != null) ...[
                  const SizedBox(height: ErpSpace.s2),
                  Directionality(
                    textDirection: TextDirection.ltr,
                    child: Text('${s.issuer} · ${s.account}', textAlign: TextAlign.center, style: theme.textTheme.bodySmall?.copyWith(color: erp.muted)),
                  ),
                ],
              ],
            ),
          ),
          const SizedBox(height: ErpSpace.s2),
          if (s != null)
            Align(
              alignment: AlignmentDirectional.centerStart,
              child: TextButton.icon(
                key: const Key('twofa-show-key'),
                style: TextButton.styleFrom(minimumSize: const Size(48, 48)),
                onPressed: () => setState(() => _showKey = !_showKey),
                icon: Icon(_showKey ? Icons.visibility_off_outlined : Icons.key_outlined),
                label: Text(_showKey ? 'Hide key' : "Can't scan? Show key"),
              ),
            ),
          if (s != null && _showKey)
            Container(
              key: const Key('twofa-key-box'),
              padding: const EdgeInsetsDirectional.only(start: ErpSpace.s3, end: ErpSpace.s1),
              decoration: BoxDecoration(
                color: erp.sunken,
                borderRadius: BorderRadius.circular(ErpRadius.input),
                border: Border.all(color: erp.border),
              ),
              child: Row(
                children: [
                  Expanded(
                    child: Directionality(
                      textDirection: TextDirection.ltr, // the key is Latin letters and digits in any language
                      child: SelectableText(
                        s.secret,
                        key: const Key('twofa-secret'),
                        style: theme.textTheme.titleSmall?.copyWith(fontFamily: 'monospace', letterSpacing: 1.2, fontWeight: FontWeight.w600),
                      ),
                    ),
                  ),
                  IconButton(
                    key: const Key('twofa-copy-key'),
                    tooltip: 'Copy key',
                    constraints: const BoxConstraints(minWidth: 48, minHeight: 48),
                    onPressed: () => _copy(s.secret.replaceAll(' ', ''), 'Key copied'),
                    icon: Icon(Icons.copy_rounded, color: erp.brand),
                  ),
                ],
              ),
            ),
          const SizedBox(height: ErpSpace.s4),
        ],
        TextField(
          key: const Key('twofa-code'),
          controller: _code,
          focusNode: _focus,
          autofocus: !setup,
          enabled: !_busy && (!setup || s != null),
          keyboardType: _useRecovery ? TextInputType.visiblePassword : TextInputType.number,
          autofillHints: const [AutofillHints.oneTimeCode],
          textAlign: TextAlign.center,
          textInputAction: TextInputAction.done,
          maxLength: _useRecovery ? 24 : 6,
          inputFormatters: _useRecovery ? const [] : [FilteringTextInputFormatter.digitsOnly, LengthLimitingTextInputFormatter(6)],
          style: theme.textTheme.headlineSmall?.copyWith(letterSpacing: _useRecovery ? 1 : 10, fontFeatures: ErpFonts.tabular),
          onChanged: _onChanged,
          onSubmitted: (_) => _submit(),
          decoration: InputDecoration(
            labelText: _useRecovery ? 'Recovery code' : '6-digit code',
            hintText: _useRecovery ? 'XXXXX-XXXXX' : '000000',
            counterText: '',
            errorText: _error,
            errorMaxLines: 3,
          ),
        ),
        const SizedBox(height: ErpSpace.s4),
        FilledButton(
          key: const Key('twofa-submit'),
          style: FilledButton.styleFrom(minimumSize: const Size.fromHeight(48)),
          onPressed: _busy || !_canSubmit ? null : _submit,
          child: _busy
              ? const SizedBox(height: 20, width: 20, child: CircularProgressIndicator(strokeWidth: 2))
              : Text(setup ? 'Verify and turn on' : 'Verify and sign in'),
        ),
        const SizedBox(height: ErpSpace.s2),
        if (!setup)
          TextButton(
            key: const Key('twofa-use-recovery'),
            style: TextButton.styleFrom(minimumSize: const Size.fromHeight(48)),
            onPressed: () => setState(() {
              _useRecovery = !_useRecovery;
              _code.clear();
              _error = null;
            }),
            child: Text(_useRecovery ? 'Use the authenticator app instead' : 'Use a recovery code'),
          ),
        TextButton(
          key: const Key('twofa-cancel'),
          style: TextButton.styleFrom(minimumSize: const Size.fromHeight(48), foregroundColor: erp.muted),
          onPressed: widget.onCancel,
          child: const Text('Back to sign in'),
        ),
      ],
    );
  }

  Widget _recoveryView(BuildContext context, TwoFactorEnabled enabled) {
    final theme = Theme.of(context);
    final erp = ErpTheme.of(context);
    return Column(
      crossAxisAlignment: CrossAxisAlignment.stretch,
      children: [
        _header(context, Icons.verified_user_outlined, 'Save your recovery codes',
            'Two-factor is on. If you lose your phone, each of these codes signs you in once. They are shown only now.'),
        Container(
          key: const Key('twofa-recovery-codes'),
          padding: const EdgeInsets.all(ErpSpace.s4),
          decoration: BoxDecoration(color: erp.sunken, borderRadius: BorderRadius.circular(ErpRadius.input), border: Border.all(color: erp.border)),
          child: Directionality(
            textDirection: TextDirection.ltr,
            child: Wrap(
              spacing: ErpSpace.s6,
              runSpacing: ErpSpace.s2,
              children: [
                for (final c in enabled.recoveryCodes)
                  SizedBox(width: 130, child: Text(c, style: theme.textTheme.titleSmall?.copyWith(fontFamily: 'monospace', fontWeight: FontWeight.w600))),
              ],
            ),
          ),
        ),
        const SizedBox(height: ErpSpace.s3),
        OutlinedButton.icon(
          key: const Key('twofa-copy-codes'),
          style: OutlinedButton.styleFrom(minimumSize: const Size.fromHeight(48)),
          onPressed: () => _copy(enabled.recoveryCodes.join('\n'), 'Recovery codes copied'),
          icon: const Icon(Icons.copy_rounded),
          label: const Text('Copy codes'),
        ),
        const SizedBox(height: ErpSpace.s2),
        FilledButton(
          key: const Key('twofa-continue'),
          style: FilledButton.styleFrom(minimumSize: const Size.fromHeight(48)),
          onPressed: _finish,
          child: const Text('Continue'),
        ),
      ],
    );
  }

  Widget _expiredView(BuildContext context) => Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: [
          _header(context, Icons.schedule_outlined, 'Sign in again', 'This sign-in step has expired. Enter your email and password again to continue.'),
          FilledButton(
            key: const Key('twofa-restart'),
            style: FilledButton.styleFrom(minimumSize: const Size.fromHeight(48)),
            onPressed: widget.onCancel,
            child: const Text('Back to sign in'),
          ),
        ],
      );
}
