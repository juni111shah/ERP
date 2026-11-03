import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../core/api_client.dart';
import '../../core/key_value_store.dart';
import '../../core/session.dart';
import '../../theme/app_theme.dart';
import '../../theme/tokens.dart';
import 'two_factor_step.dart';

class LoginPage extends ConsumerStatefulWidget {
  const LoginPage({super.key});

  @override
  ConsumerState<LoginPage> createState() => _LoginPageState();
}

class _LoginPageState extends ConsumerState<LoginPage> {
  final _form = GlobalKey<FormState>();
  final _email = TextEditingController();
  final _password = TextEditingController();
  final _code = TextEditingController();
  late final TextEditingController _server;
  bool _remember = true;
  bool _needsCode = false;
  bool _busy = false;
  bool _showPassword = false;
  String? _error;

  /// Set after a correct password when the account must set up, or confirm with, two-factor.
  (TwoFactorMode, String)? _challenge;

  @override
  void initState() {
    super.initState();
    _server = TextEditingController(text: ref.read(serverUrlProvider));
    ref.read(storeProvider).read(StoreKeys.lastEmail).then((v) {
      if (mounted && v != null && _email.text.isEmpty) _email.text = v;
    });
  }

  @override
  void dispose() {
    _email.dispose();
    _password.dispose();
    _code.dispose();
    _server.dispose();
    super.dispose();
  }

  Future<void> _submit() async {
    if (!_form.currentState!.validate()) return;
    setState(() {
      _busy = true;
      _error = null;
    });
    try {
      await ref.read(serverUrlProvider.notifier).set(_server.text);
      final outcome = await ref.read(sessionProvider.notifier).login(
            email: _email.text,
            password: _password.text,
            rememberMe: _remember,
            twoFactorCode: _needsCode ? _code.text : null,
          );
      switch (outcome) {
        case LoggedIn():
          break; // the router moves on once the session exists
        case NeedsTwoFactorSetup(:final challengeToken):
          setState(() => _challenge = (TwoFactorMode.setup, challengeToken));
        case NeedsTwoFactorCode(:final challengeToken):
          setState(() => _challenge = (TwoFactorMode.verify, challengeToken));
        case TwoFactorEnabled():
          break; // only returned by verifyTwoFactor
        case NeedsTwoFactor():
          setState(() {
            _needsCode = true;
            if (_code.text.isNotEmpty) _error = 'That code was not accepted. Try the next one.';
          });
        case NeedsWorkspace(:final selectionToken, :final workspaces):
          ref.read(pendingWorkspaceProvider.notifier).set(PendingWorkspaceSelection(selectionToken, workspaces));
      }
    } on ApiException catch (e) {
      setState(() => _error = e.message);
    } finally {
      if (mounted) setState(() => _busy = false);
    }
  }

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    return Scaffold(
      body: SafeArea(
        child: Center(
          child: SingleChildScrollView(
            padding: const EdgeInsets.all(ErpSpace.s6),
            child: ConstrainedBox(
              constraints: const BoxConstraints(maxWidth: 420),
              child: Form(
                key: _form,
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.stretch,
                  children: [
                    Center(
                      child: Container(
                        width: 64,
                        height: 64,
                        decoration: BoxDecoration(
                          gradient: ErpTheme.of(context).heroGradient,
                          borderRadius: BorderRadius.circular(18),
                          boxShadow: const [BoxShadow(color: Color(0x3310955D), blurRadius: 18, offset: Offset(0, 8))],
                        ),
                        child: const Icon(Icons.account_balance_outlined, size: 32, color: Colors.white),
                      ),
                    ),
                    const SizedBox(height: ErpSpace.s4),
                    Text('ERP', textAlign: TextAlign.center, style: theme.textTheme.headlineMedium?.copyWith(fontWeight: FontWeight.w700, letterSpacing: -0.5)),
                    const SizedBox(height: ErpSpace.s1),
                    Text('Sign in to your workspace', textAlign: TextAlign.center, style: theme.textTheme.bodyMedium?.copyWith(color: ErpTheme.of(context).muted)),
                    const SizedBox(height: 28),
                    if (_challenge case (final mode, final token))
                      TwoFactorStep(
                        key: ValueKey(token),
                        mode: mode,
                        challengeToken: token,
                        onCancel: () => setState(() {
                          _challenge = null;
                          _password.clear();
                        }),
                      )
                    else ...[
                    TextFormField(
                      controller: _email,
                      keyboardType: TextInputType.emailAddress,
                      autofillHints: const [AutofillHints.username, AutofillHints.email],
                      textInputAction: TextInputAction.next,
                      decoration: const InputDecoration(labelText: 'Email', prefixIcon: Icon(Icons.mail_outline)),
                      validator: (v) => (v == null || !v.contains('@')) ? 'Enter your email address' : null,
                    ),
                    const SizedBox(height: ErpSpace.s35),
                    TextFormField(
                      controller: _password,
                      obscureText: !_showPassword,
                      autofillHints: const [AutofillHints.password],
                      textInputAction: _needsCode ? TextInputAction.next : TextInputAction.done,
                      onFieldSubmitted: (_) => _needsCode ? null : _submit(),
                      decoration: InputDecoration(
                        labelText: 'Password',
                        prefixIcon: const Icon(Icons.lock_outline),
                        suffixIcon: IconButton(
                          tooltip: _showPassword ? 'Hide password' : 'Show password',
                          icon: Icon(_showPassword ? Icons.visibility_off_outlined : Icons.visibility_outlined),
                          onPressed: () => setState(() => _showPassword = !_showPassword),
                        ),
                      ),
                      validator: (v) => (v == null || v.isEmpty) ? 'Enter your password' : null,
                    ),
                    if (_needsCode) ...[
                      const SizedBox(height: ErpSpace.s35),
                      TextFormField(
                        controller: _code,
                        keyboardType: TextInputType.number,
                        autofillHints: const [AutofillHints.oneTimeCode],
                        onFieldSubmitted: (_) => _submit(),
                        decoration: const InputDecoration(
                          labelText: 'Authenticator or recovery code',
                          prefixIcon: Icon(Icons.shield_outlined),
                        ),
                        validator: (v) => (v == null || v.trim().isEmpty) ? 'Enter the code from your authenticator app' : null,
                      ),
                    ],
                    CheckboxListTile(
                      value: _remember,
                      onChanged: (v) => setState(() => _remember = v ?? true),
                      title: const Text('Keep me signed in'),
                      controlAffinity: ListTileControlAffinity.leading,
                      contentPadding: EdgeInsets.zero,
                    ),
                    if (_error != null)
                      Padding(
                        padding: const EdgeInsets.only(bottom: ErpSpace.s3),
                        child: Text(_error!, style: TextStyle(color: theme.colorScheme.error)),
                      ),
                    FilledButton(
                      onPressed: _busy ? null : _submit,
                      child: Padding(
                        padding: const EdgeInsets.symmetric(vertical: ErpSpace.s3),
                        child: _busy
                            ? const SizedBox(height: 20, width: 20, child: CircularProgressIndicator(strokeWidth: 2))
                            : Text(_needsCode ? 'Verify and sign in' : 'Sign in'),
                      ),
                    ),
                    const SizedBox(height: ErpSpace.s2),
                    ExpansionTile(
                      title: const Text('Server'),
                      leading: const Icon(Icons.dns_outlined),
                      shape: const Border(),
                      collapsedShape: const Border(),
                      children: [
                        Padding(
                          padding: const EdgeInsets.only(bottom: ErpSpace.s2),
                          child: TextFormField(
                            controller: _server,
                            keyboardType: TextInputType.url,
                            decoration: const InputDecoration(labelText: 'Server address', hintText: 'https://erp.example.com'),
                            validator: (v) => (v == null || !(Uri.tryParse(v.trim())?.hasScheme ?? false)) ? 'Start with http:// or https://' : null,
                          ),
                        ),
                      ],
                    ),
                    ],
                  ],
                ),
              ),
            ),
          ),
        ),
      ),
    );
  }
}
