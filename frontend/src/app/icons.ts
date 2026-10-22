import {
  AccountBookOutline, ApartmentOutline, BarChartOutline, BookOutline, DeleteOutline, DollarOutline, LockOutline, WalletOutline, AppstoreAddOutline, AppstoreOutline, AuditOutline, BankOutline, BellOutline,
  BulbOutline, CheckSquareOutline, ClusterOutline, DashboardOutline, FileTextOutline, FormOutline, FundOutline,
  HomeOutline, KeyOutline, LogoutOutline, MenuFoldOutline, MenuUnfoldOutline, PercentageOutline, PlusOutline,
  SafetyOutline, SearchOutline, SettingOutline, ShoppingOutline, SwapOutline, TeamOutline, ToolOutline, UserOutline,
} from '@ant-design/icons-angular/icons';

/** Icons used by the shell and the server-driven menu (MenuItemDefinition.Icon). Unknown names fall back to "appstore". */
export const APP_ICONS = [
  AccountBookOutline, ApartmentOutline, BarChartOutline, BookOutline, DeleteOutline, DollarOutline, LockOutline, WalletOutline, AppstoreAddOutline, AppstoreOutline, AuditOutline, BankOutline, BellOutline,
  BulbOutline, CheckSquareOutline, ClusterOutline, DashboardOutline, FileTextOutline, FormOutline, FundOutline,
  HomeOutline, KeyOutline, LogoutOutline, MenuFoldOutline, MenuUnfoldOutline, PercentageOutline, PlusOutline,
  SafetyOutline, SearchOutline, SettingOutline, ShoppingOutline, SwapOutline, TeamOutline, ToolOutline, UserOutline,
];
export const KNOWN_ICONS = new Set(APP_ICONS.map((i) => i.name));
