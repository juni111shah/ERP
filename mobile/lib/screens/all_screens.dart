import '../features/crm/screens.dart';
import '../features/hr/screens.dart';
import '../features/ops/screens.dart';
import '../features/payables/screens.dart';
import '../features/projects/screens.dart';
import '../features/purchasing/screens.dart';
import '../features/realestate/screens.dart';
import '../features/receivables/screens.dart';
import '../features/sales/screens.dart';
import '../kit/registry.dart';

/// Every screen the app has, by area. The More screen matches these to the server menu by route.
final List<MobileScreen> allScreens = <MobileScreen>[
  ...receivablesScreens,
  ...payablesScreens,
  ...salesScreens,
  ...opsScreens,
  ...purchasingScreens,
  ...hrScreens,
  ...crmScreens,
  ...projectsScreens,
  ...realestateScreens,
];
