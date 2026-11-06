import {
  GlobalOutline, AccountBookOutline, ApartmentOutline, BarChartOutline, DownloadOutline, PrinterOutline, CreditCardOutline, BookOutline, DeleteOutline, DollarOutline, LockOutline, WalletOutline, AppstoreAddOutline, AppstoreOutline, AuditOutline, BankOutline, BellOutline,
  BulbOutline, CheckSquareOutline, ClusterOutline, DashboardOutline, FileTextOutline, FormOutline, FundOutline,
  HomeOutline, KeyOutline, LogoutOutline, MenuFoldOutline, MenuUnfoldOutline, PercentageOutline, PlusOutline,
  SafetyOutline, SearchOutline, SettingOutline, ShoppingOutline, ShopOutline, SwapOutline, TeamOutline, ToolOutline, UserOutline, MailOutline, WarningOutline, CheckCircleOutline,
  CheckOutline, CopyOutline, EditOutline, FileAddOutline, LinkOutline, ReloadOutline, RollbackOutline, StopOutline, ThunderboltOutline,
  DoubleLeftOutline, DoubleRightOutline, LeftOutline, RightOutline, FilterOutline,
  HolderOutline, UndoOutline, VerticalLeftOutline, VerticalRightOutline, ClearOutline,
  FallOutline, FolderOpenOutline, PartitionOutline, RiseOutline, UnorderedListOutline,
  ProjectOutline, ClockCircleOutline, DollarCircleOutline,
  ShoppingCartOutline, LineChartOutline, ArrowDownOutline, ArrowUpOutline, MinusOutline,
  ArrowLeftOutline, BarcodeOutline, CalculatorOutline, CalendarOutline, CommentOutline, EyeOutline, FieldTimeOutline, FileDoneOutline, FlagOutline, HistoryOutline, InboxOutline, MoreOutline,
  PaperClipOutline, PauseCircleOutline, PlayCircleOutline, SaveOutline, ScheduleOutline, SendOutline, TableOutline, TagOutline, TransactionOutline, UserAddOutline, ZoomInOutline, ZoomOutOutline,
  AlertOutline, ArrowRightOutline, CarOutline, CheckCircleFill, CloseCircleFill, CloseCircleOutline, CloseOutline, CompassOutline, DesktopOutline, DownOutline, EnvironmentOutline, ExperimentOutline, EyeInvisibleOutline, FileSearchOutline, FilterFill, FrownOutline, GiftOutline, InfoCircleOutline, MinusCircleOutline, PhoneOutline, PoweroffOutline, ProfileOutline, SafetyCertificateFill, SafetyCertificateOutline, SolutionOutline, StepForwardOutline, SyncOutline, ThunderboltFill, TrophyOutline, UploadOutline,
  ContactsOutline, CustomerServiceOutline, DatabaseOutline, IdcardOutline, ExportOutline, BugOutline, FileProtectOutline, GoldOutline, ImportOutline, MedicineBoxOutline, ReadOutline, StockOutline,
} from '@ant-design/icons-angular/icons';

/** Icons used by the shell and the server-driven menu (MenuItemDefinition.Icon). Unknown names fall back to "appstore". */
export const APP_ICONS = [
  GlobalOutline,
  AccountBookOutline, ApartmentOutline, BarChartOutline, DownloadOutline, PrinterOutline, CreditCardOutline, BookOutline, DeleteOutline, DollarOutline, LockOutline, WalletOutline, AppstoreAddOutline, AppstoreOutline, AuditOutline, BankOutline, BellOutline,
  BulbOutline, CheckSquareOutline, ClusterOutline, DashboardOutline, FileTextOutline, FormOutline, FundOutline,
  HomeOutline, KeyOutline, LogoutOutline, MenuFoldOutline, MenuUnfoldOutline, PercentageOutline, PlusOutline,
  SafetyOutline, SearchOutline, SettingOutline, ShoppingOutline, ShopOutline, SwapOutline, TeamOutline, ToolOutline, UserOutline, MailOutline, WarningOutline, CheckCircleOutline,
  ProjectOutline, ClockCircleOutline, DollarCircleOutline,
  ShoppingCartOutline, LineChartOutline,
  // Stock movements: in, out, no change.
  ArrowDownOutline, ArrowUpOutline, MinusOutline,
  // Toolbar and detail-page action icons (detail-page actions pass icon names through ActionDef.icon).
  CheckOutline, CopyOutline, EditOutline, FileAddOutline, LinkOutline, ReloadOutline, RollbackOutline, StopOutline, ThunderboltOutline,
  // Pagination bar and collapsed filter controls.
  DoubleLeftOutline, DoubleRightOutline, LeftOutline, RightOutline, FilterOutline,
  // Table column panel (drag handle, pin start / end, reset layout) and Clear filters: unregistered icons are fetched from /assets and 404.
  HolderOutline, UndoOutline, VerticalLeftOutline, VerticalRightOutline, ClearOutline,
  // Step-by-step chart of accounts (category icons, column chevrons, view toggle).
  FallOutline, FolderOpenOutline, PartitionOutline, RiseOutline, UnorderedListOutline,
  // Projects screens, and module menu icons that were fetched from /assets and failed.
  ArrowLeftOutline, BarcodeOutline, CalculatorOutline, CalendarOutline, CommentOutline, EyeOutline, FieldTimeOutline, FileDoneOutline, FlagOutline, HistoryOutline, InboxOutline, MoreOutline,
  PaperClipOutline, PauseCircleOutline, PlayCircleOutline, SaveOutline, ScheduleOutline, SendOutline, TableOutline, TagOutline, TransactionOutline, UserAddOutline, ZoomInOutline, ZoomOutOutline,
  AlertOutline, ArrowRightOutline, CarOutline, CheckCircleFill, CloseCircleFill, CloseCircleOutline, CloseOutline, CompassOutline, DesktopOutline, DownOutline, EnvironmentOutline, ExperimentOutline, EyeInvisibleOutline, FileSearchOutline, FilterFill, FrownOutline, GiftOutline, InfoCircleOutline, MinusCircleOutline, PhoneOutline, PoweroffOutline, ProfileOutline, SafetyCertificateFill, SafetyCertificateOutline, SolutionOutline, StepForwardOutline, SyncOutline, ThunderboltFill, TrophyOutline, UploadOutline,
  // Sidebar category icons (see layout/sidebar-nav.ts).
  ContactsOutline, CustomerServiceOutline, DatabaseOutline, IdcardOutline, ExportOutline, BugOutline, FileProtectOutline, GoldOutline, ImportOutline, MedicineBoxOutline, ReadOutline, StockOutline,
];
export const KNOWN_ICONS = new Set(APP_ICONS.map((i) => i.name));
