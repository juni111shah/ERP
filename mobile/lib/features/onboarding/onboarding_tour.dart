import 'package:flutter/material.dart';

import '../../core/key_value_store.dart';
import '../../theme/app_theme.dart';
import '../../theme/tokens.dart';

// First-time guided tour: four short cards over the signed-in shell.
//
// * Shown once after the first sign-in ([OnboardingTour.maybeShowAfterSignIn]).
// * Skip, Back, Next / Done, and a "Don't show this again" switch (on by default).
// * The seen flag lives in the app's existing [KeyValueStore] (secure storage on device,
//   [MemoryKeyValueStore] in tests); no new packages.
// * "Show the tour again" from the profile sheet: [ShowTourAgainTile] or [OnboardingTour.showAgain].
// * RTL: Directional insets/alignment only; PageView follows [Directionality]. Dark mode: colours
//   come from [ErpTheme.of] (the app's light/dark token set built from tokens.dart).

/// Text for the tour. The app has no l10n framework yet (screens pick Arabic with
/// `Localizations.localeOf(context).languageCode == 'ar'`), so the English and Arabic sets live here
/// and [OnboardingStrings.of] picks one by locale. Pass your own to override.
@immutable
class OnboardingStrings {
  const OnboardingStrings({
    required this.tourLabel,
    required this.skip,
    required this.back,
    required this.next,
    required this.done,
    required this.dontShowAgain,
    required this.stepOf,
    required this.showTourAgain,
    required this.showTourAgainHint,
    required this.steps,
  });

  /// Screen-reader name of the tour dialog.
  final String tourLabel;
  final String skip;
  final String back;
  final String next;
  final String done;
  final String dontShowAgain;

  /// Progress text with `{n}` and `{total}` placeholders, e.g. "Step {n} of {total}".
  final String stepOf;

  /// Profile-sheet row.
  final String showTourAgain;
  final String showTourAgainHint;

  /// Title/body of each step, in the order of [OnboardingTour.defaultSteps] icons.
  final List<OnboardingStepText> steps;

  String progress(int n, int total) => stepOf.replaceAll('{n}', '$n').replaceAll('{total}', '$total');

  static const en = OnboardingStrings(
    tourLabel: 'Welcome tour',
    skip: 'Skip',
    back: 'Back',
    next: 'Next',
    done: 'Done',
    dontShowAgain: "Don't show this again",
    stepOf: 'Step {n} of {total}',
    showTourAgain: 'Show the tour again',
    showTourAgainHint: 'A quick look at the main screen',
    steps: [
      OnboardingStepText(
        'Your key numbers',
        'The Highlights cards on the Overview show the numbers that matter most right now. Tap a card to open the details behind it.',
      ),
      OnboardingStepText(
        'Swipe for more',
        'Swipe the cards sideways to see more numbers. Use the chips above them to show one area at a time, like Finance or Sales.',
      ),
      OnboardingStepText(
        'Your profile menu',
        'Tap your picture at the top to switch workspace, choose light or dark, open settings or sign out. You can replay this tour from there.',
      ),
      OnboardingStepText(
        'Jump anywhere',
        'Tap the search icon at the top (or long-press Overview) and type a few letters to open any screen, instead of digging through menus.',
      ),
    ],
  );

  static const ar = OnboardingStrings(
    tourLabel: 'جولة ترحيبية',
    skip: 'تخطٍّ',
    back: 'السابق',
    next: 'التالي',
    done: 'تم',
    dontShowAgain: 'لا تعرض هذا مرة أخرى',
    stepOf: 'الخطوة {n} من {total}',
    showTourAgain: 'عرض الجولة مرة أخرى',
    showTourAgainHint: 'نظرة سريعة على الشاشة الرئيسية',
    steps: [
      OnboardingStepText(
        'أرقامك الرئيسية',
        'تعرض بطاقات «أبرز الأرقام» في النظرة العامة أهم الأرقام الآن. اضغط على أي بطاقة لفتح تفاصيلها.',
      ),
      OnboardingStepText(
        'اسحب لرؤية المزيد',
        'اسحب البطاقات جانبًا لرؤية أرقام أخرى. استخدم الأزرار أعلاها لعرض مجال واحد في كل مرة، مثل المالية أو المبيعات.',
      ),
      OnboardingStepText(
        'قائمة ملفك الشخصي',
        'اضغط على صورتك في الأعلى لتبديل مساحة العمل أو اختيار المظهر الفاتح أو الداكن أو فتح الإعدادات أو تسجيل الخروج. ويمكنك إعادة هذه الجولة من هناك.',
      ),
      OnboardingStepText(
        'انتقل إلى أي مكان',
        'اضغط على أيقونة البحث في الأعلى (أو اضغط مطولًا على النظرة العامة) واكتب بضعة أحرف لفتح أي شاشة، بدلًا من التنقل بين القوائم.',
      ),
    ],
  );

  /// Arabic when the app locale is Arabic or the surrounding text direction is RTL, else English.
  /// (At 5595eb3 MaterialApp has no supportedLocales, so the locale alone is always English;
  /// the RTL check lets an RTL build pick Arabic too.)
  static OnboardingStrings of(BuildContext context) {
    final locale = Localizations.maybeLocaleOf(context);
    final rtl = Directionality.maybeOf(context) == TextDirection.rtl;
    return locale?.languageCode == 'ar' || rtl ? ar : en;
  }
}

@immutable
class OnboardingStepText {
  const OnboardingStepText(this.title, this.body);

  final String title;
  final String body;
}

/// One card of the tour.
@immutable
class OnboardingStep {
  const OnboardingStep({required this.icon, required this.title, required this.body});

  final IconData icon;
  final String title;
  final String body;
}

/// How the tour ended.
enum OnboardingOutcome { completed, skipped, dismissed }

/// Persistence of the "seen" flag in the app's existing [KeyValueStore].
class OnboardingTourStore {
  const OnboardingTourStore(this.store, {this.scope});

  /// Bump the suffix when the tour content changes enough to show it to everyone again.
  static const seenKey = 'onboarding_tour_v1_seen';

  final KeyValueStore store;

  /// Optional per-user/per-workspace scope (e.g. the user id), so a shared phone shows it per person.
  final String? scope;

  String get key => scope == null || scope!.isEmpty ? seenKey : '${seenKey}_$scope';

  Future<bool> isSeen() async {
    try {
      return await store.read(key) == '1';
    } catch (_) {
      // Storage unavailable: treat as seen so the tour never blocks the app.
      return true;
    }
  }

  Future<void> markSeen() async {
    try {
      await store.write(key, '1');
    } catch (_) {
      // Still closed for this run.
    }
  }

  Future<void> reset() async {
    try {
      await store.delete(key);
    } catch (_) {
      // Nothing to reset.
    }
  }
}

/// The tour card (PageView of steps + controls). Usually opened with [OnboardingTour.show].
class OnboardingTour extends StatefulWidget {
  const OnboardingTour({
    super.key,
    required this.steps,
    required this.strings,
    required this.onFinished,
    this.initialDontShowAgain = true,
  });

  final List<OnboardingStep> steps;
  final OnboardingStrings strings;

  /// Called once with how the tour ended and the state of "Don't show this again".
  final void Function(OnboardingOutcome outcome, bool dontShowAgain) onFinished;

  final bool initialDontShowAgain;

  static const _icons = [
    Icons.insights_outlined, // Overview highlights
    Icons.swipe_outlined, // carousel swipe + category chips
    Icons.account_circle_outlined, // profile menu
    Icons.search_rounded, // quick search / command palette
  ];

  /// The four default steps with [strings] text.
  static List<OnboardingStep> defaultSteps(OnboardingStrings strings) => [
        for (var i = 0; i < strings.steps.length; i++)
          OnboardingStep(icon: _icons[i % _icons.length], title: strings.steps[i].title, body: strings.steps[i].body),
      ];

  static bool _showing = false;

  /// First-sign-in hook: shows the tour unless it was already seen. Returns true when it was shown.
  /// Safe to call on every arrival at the home shell.
  static Future<bool> maybeShowAfterSignIn(
    BuildContext context,
    KeyValueStore store, {
    String? scope,
    OnboardingStrings? strings,
    List<OnboardingStep>? steps,
  }) async {
    if (_showing) return false;
    final flag = OnboardingTourStore(store, scope: scope);
    if (await flag.isSeen()) return false;
    if (!context.mounted) return false;
    await show(context, store, scope: scope, strings: strings, steps: steps);
    return true;
  }

  /// Opens the tour as a dialog. When it closes with "Don't show this again" on, the flag is saved.
  static Future<OnboardingOutcome> show(
    BuildContext context,
    KeyValueStore store, {
    String? scope,
    OnboardingStrings? strings,
    List<OnboardingStep>? steps,
  }) async {
    if (_showing) return OnboardingOutcome.dismissed;
    _showing = true;
    final s = strings ?? OnboardingStrings.of(context);
    final flag = OnboardingTourStore(store, scope: scope);
    var outcome = OnboardingOutcome.dismissed;
    var dontShow = true;
    try {
      await showDialog<void>(
        context: context,
        barrierDismissible: false,
        builder: (ctx) => PopScope(
          // System back = Skip.
          canPop: false,
          onPopInvokedWithResult: (didPop, _) {
            if (!didPop) {
              outcome = OnboardingOutcome.skipped;
              Navigator.of(ctx).pop();
            }
          },
          child: Dialog(
            // Dialog only accepts EdgeInsets; symmetric is direction-neutral.
            insetPadding: const EdgeInsets.symmetric(horizontal: ErpSpace.s4, vertical: ErpSpace.s6),
            shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(ErpRadius.panel)),
            clipBehavior: Clip.antiAlias,
            child: OnboardingTour(
              steps: steps ?? defaultSteps(s),
              strings: s,
              onFinished: (o, d) {
                outcome = o;
                dontShow = d;
                Navigator.of(ctx).pop();
              },
            ),
          ),
        ),
      );
      if (dontShow) await flag.markSeen();
    } finally {
      _showing = false;
    }
    return outcome;
  }

  /// Clears the flag so the tour shows again on the next sign-in.
  static Future<void> reset(KeyValueStore store, {String? scope}) => OnboardingTourStore(store, scope: scope).reset();

  /// "Show the tour again": clears the flag and opens the tour now.
  static Future<OnboardingOutcome> showAgain(BuildContext context, KeyValueStore store, {String? scope, OnboardingStrings? strings}) async {
    await reset(store, scope: scope);
    if (!context.mounted) return OnboardingOutcome.dismissed;
    return show(context, store, scope: scope, strings: strings);
  }

  @override
  State<OnboardingTour> createState() => _OnboardingTourState();
}

class _OnboardingTourState extends State<OnboardingTour> {
  final _pages = PageController();
  int _index = 0;
  late bool _dontShowAgain = widget.initialDontShowAgain;
  bool _finished = false;

  bool get _isLast => _index >= widget.steps.length - 1;

  @override
  void dispose() {
    _pages.dispose();
    super.dispose();
  }

  void _finish(OnboardingOutcome outcome) {
    if (_finished) return;
    _finished = true;
    widget.onFinished(outcome, _dontShowAgain);
  }

  void _go(int to) {
    if (to < 0 || to >= widget.steps.length) return;
    _pages.animateToPage(to, duration: const Duration(milliseconds: 280), curve: Curves.easeInOut);
  }

  @override
  Widget build(BuildContext context) {
    final t = ErpTheme.of(context);
    final s = widget.strings;
    final total = widget.steps.length;
    final buttonShape = RoundedRectangleBorder(borderRadius: BorderRadius.circular(ErpRadius.input));

    return Semantics(
      key: const ValueKey('onboarding-tour'),
      scopesRoute: true,
      namesRoute: true,
      explicitChildNodes: true,
      label: s.tourLabel,
      child: ColoredBox(
        color: t.surface,
        child: ConstrainedBox(
          constraints: const BoxConstraints(maxWidth: 440, maxHeight: 560),
          child: Column(
            mainAxisSize: MainAxisSize.min,
            children: [
              // Progress + Skip.
              Padding(
                padding: const EdgeInsetsDirectional.fromSTEB(ErpSpace.s5, ErpSpace.s2, ErpSpace.s2, 0),
                child: Row(children: [
                  Expanded(
                    child: Text(
                      s.progress(_index + 1, total),
                      key: const ValueKey('onboarding-progress'),
                      style: TextStyle(fontSize: 12, fontWeight: FontWeight.w600, color: t.muted),
                    ),
                  ),
                  TextButton(
                    key: const ValueKey('onboarding-skip'),
                    style: TextButton.styleFrom(minimumSize: const Size(64, 48), foregroundColor: t.text2),
                    onPressed: () => _finish(OnboardingOutcome.skipped),
                    child: Text(s.skip),
                  ),
                ]),
              ),
              Flexible(
                child: SizedBox(
                  height: 300,
                  child: PageView.builder(
                    key: const ValueKey('onboarding-pages'),
                    controller: _pages,
                    itemCount: total,
                    onPageChanged: (i) => setState(() => _index = i),
                    itemBuilder: (context, i) => _StepView(step: widget.steps[i], index: i),
                  ),
                ),
              ),
              // Page dots.
              Padding(
                padding: const EdgeInsetsDirectional.only(top: ErpSpace.s2, bottom: ErpSpace.s2),
                child: ExcludeSemantics(
                  child: Row(
                    mainAxisAlignment: MainAxisAlignment.center,
                    children: [
                      for (var i = 0; i < total; i++)
                        AnimatedContainer(
                          duration: const Duration(milliseconds: 200),
                          margin: const EdgeInsetsDirectional.symmetric(horizontal: 3),
                          width: i == _index ? 18 : 6,
                          height: 6,
                          decoration: BoxDecoration(
                            color: i == _index ? t.brand : t.border,
                            borderRadius: BorderRadius.circular(ErpRadius.pill),
                          ),
                        ),
                    ],
                  ),
                ),
              ),
              // Don't show again.
              MergeSemantics(
                child: InkWell(
                  key: const ValueKey('onboarding-dont-show'),
                  onTap: () => setState(() => _dontShowAgain = !_dontShowAgain),
                  child: ConstrainedBox(
                    constraints: const BoxConstraints(minHeight: 48),
                    child: Padding(
                      padding: const EdgeInsetsDirectional.symmetric(horizontal: ErpSpace.s3),
                      child: Row(children: [
                        Checkbox(
                          value: _dontShowAgain,
                          activeColor: t.brand,
                          onChanged: (v) => setState(() => _dontShowAgain = v ?? false),
                        ),
                        const SizedBox(width: ErpSpace.s1),
                        Expanded(child: Text(s.dontShowAgain, style: TextStyle(fontSize: 14, color: t.text2))),
                      ]),
                    ),
                  ),
                ),
              ),
              // Back / Next-Done.
              Padding(
                padding: const EdgeInsetsDirectional.fromSTEB(ErpSpace.s4, ErpSpace.s1, ErpSpace.s4, ErpSpace.s4),
                child: Row(children: [
                  if (_index > 0) ...[
                    Expanded(
                      child: OutlinedButton(
                        key: const ValueKey('onboarding-back'),
                        style: OutlinedButton.styleFrom(
                          minimumSize: const Size.fromHeight(48),
                          shape: buttonShape,
                          foregroundColor: t.text,
                          side: BorderSide(color: t.border),
                        ),
                        onPressed: () => _go(_index - 1),
                        child: Text(s.back),
                      ),
                    ),
                    const SizedBox(width: ErpSpace.s3),
                  ],
                  Expanded(
                    child: FilledButton(
                      key: const ValueKey('onboarding-next'),
                      style: FilledButton.styleFrom(
                        minimumSize: const Size.fromHeight(48),
                        shape: buttonShape,
                        backgroundColor: t.brand,
                        foregroundColor: Colors.white,
                      ),
                      onPressed: _isLast ? () => _finish(OnboardingOutcome.completed) : () => _go(_index + 1),
                      child: Text(_isLast ? s.done : s.next),
                    ),
                  ),
                ]),
              ),
            ],
          ),
        ),
      ),
    );
  }
}

class _StepView extends StatelessWidget {
  const _StepView({required this.step, required this.index});

  final OnboardingStep step;
  final int index;

  @override
  Widget build(BuildContext context) {
    final t = ErpTheme.of(context);
    return SingleChildScrollView(
      key: ValueKey('onboarding-step-$index'),
      padding: const EdgeInsetsDirectional.fromSTEB(ErpSpace.s6, ErpSpace.s4, ErpSpace.s6, ErpSpace.s2),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Container(
            width: 64,
            height: 64,
            alignment: AlignmentDirectional.center,
            decoration: BoxDecoration(color: t.brandSubtle, borderRadius: BorderRadius.circular(ErpRadius.panel)),
            child: Icon(step.icon, size: 32, color: t.brand),
          ),
          const SizedBox(height: ErpSpace.s5),
          Semantics(
            header: true,
            child: Text(step.title, style: TextStyle(fontSize: 20, fontWeight: FontWeight.w700, color: t.text)),
          ),
          const SizedBox(height: ErpSpace.s2),
          Text(step.body, style: TextStyle(fontSize: 15, height: 1.45, color: t.text2)),
        ],
      ),
    );
  }
}

/// Ready-made profile-sheet row: "Show the tour again". Matches the sheet's rows (52dp, 22dp icon,
/// 15sp label, end chevron). In the sheet pass `hostContext` so the tour opens after the sheet closes.
class ShowTourAgainTile extends StatelessWidget {
  const ShowTourAgainTile({
    super.key,
    required this.store,
    this.hostContext,
    this.closeSheet = true,
    this.scope,
    this.strings,
    this.icon = Icons.tour_outlined,
  });

  final KeyValueStore store;

  /// The context that outlives the sheet (ProfileSheet.hostContext). Defaults to this tile's context.
  final BuildContext? hostContext;

  /// Pop the enclosing sheet first.
  final bool closeSheet;
  final String? scope;
  final OnboardingStrings? strings;
  final IconData icon;

  Future<void> _onTap(BuildContext context) async {
    final target = hostContext ?? context;
    final s = strings ?? OnboardingStrings.of(context);
    if (closeSheet) await Navigator.of(context).maybePop();
    if (!target.mounted) return;
    await OnboardingTour.showAgain(target, store, scope: scope, strings: s);
  }

  @override
  Widget build(BuildContext context) {
    final t = ErpTheme.of(context);
    final s = strings ?? OnboardingStrings.of(context);
    return Semantics(
      button: true,
      label: s.showTourAgain,
      hint: s.showTourAgainHint,
      excludeSemantics: true,
      child: InkWell(
        key: const ValueKey('profile-tour'),
        onTap: () => _onTap(context),
        child: ConstrainedBox(
          constraints: const BoxConstraints(minHeight: 52),
          child: Padding(
            padding: const EdgeInsetsDirectional.symmetric(horizontal: ErpSpace.s4),
            child: Row(children: [
              Icon(icon, size: 22, color: t.text2),
              const SizedBox(width: ErpSpace.s4),
              Expanded(child: Text(s.showTourAgain, style: TextStyle(fontSize: 15, fontWeight: FontWeight.w500, color: t.text))),
              Icon(Icons.chevron_right_rounded, size: 20, color: t.muted),
            ]),
          ),
        ),
      ),
    );
  }
}
