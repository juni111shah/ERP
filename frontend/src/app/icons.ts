import {
  AccountBookOutline, ApartmentOutline, BarChartOutline, DownloadOutline, PrinterOutline, CreditCardOutline, BookOutline, DeleteOutline, DollarOutline, LockOutline, WalletOutline, AppstoreAddOutline, AppstoreOutline, AuditOutline, BankOutline, BellOutline,
  BulbOutline, CheckSquareOutline, ClusterOutline, DashboardOutline, FileTextOutline, FormOutline, FundOutline,
  HomeOutline, KeyOutline, LogoutOutline, MenuFoldOutline, MenuUnfoldOutline, PercentageOutline, PlusOutline,
  SafetyOutline, SearchOutline, SettingOutline, ShoppingOutline, SwapOutline, TeamOutline, ToolOutline, UserOutline, MailOutline, WarningOutline, CheckCircleOutline,
  CheckOutline, CopyOutline, EditOutline, FileAddOutline, LinkOutline, ReloadOutline, RollbackOutline, StopOutline, ThunderboltOutline,
  DoubleLeftOutline, DoubleRightOutline, LeftOutline, RightOutline, FilterOutline,
  HolderOutline, UndoOutline, VerticalLeftOutline, VerticalRightOutline, ClearOutline,
} from '@ant-design/icons-angular/icons';

/** Icons used by the shell and the server-driven menu (MenuItemDefinition.Icon). Unknown names fall back to "appstore". */
export const APP_ICONS = [
  AccountBookOutline, ApartmentOutline, BarChartOutline, DownloadOutline, PrinterOutline, CreditCardOutline, BookOutline, DeleteOutline, DollarOutline, LockOutline, WalletOutline, AppstoreAddOutline, AppstoreOutline, AuditOutline, BankOutline, BellOutline,
  BulbOutline, CheckSquareOutline, ClusterOutline, DashboardOutline, FileTextOutline, FormOutline, FundOutline,
  HomeOutline, KeyOutline, LogoutOutline, MenuFoldOutline, MenuUnfoldOutline, PercentageOutline, PlusOutline,
  SafetyOutline, SearchOutline, SettingOutline, ShoppingOutline, SwapOutline, TeamOutline, ToolOutline, UserOutline, MailOutline, WarningOutline, CheckCircleOutline,
  // Toolbar and detail-page action icons (detail-page actions pass icon names through ActionDef.icon).
  CheckOutline, CopyOutline, EditOutline, FileAddOutline, LinkOutline, ReloadOutline, RollbackOutline, StopOutline, ThunderboltOutline,
  // Pagination bar and collapsed filter controls.
  DoubleLeftOutline, DoubleRightOutline, LeftOutline, RightOutline, FilterOutline,
  // Table column panel (drag handle, pin start / end, reset layout) and Clear filters: unregistered icons are fetched from /assets and 404.
  HolderOutline, UndoOutline, VerticalLeftOutline, VerticalRightOutline, ClearOutline,
];
export const KNOWN_ICONS = new Set(APP_ICONS.map((i) => i.name));
