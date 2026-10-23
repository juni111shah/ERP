/** UI strings by key. English is the source language: `t(key, fallback)` returns the fallback (or the key) when a translation is missing. */
export type Lang = 'en' | 'ar';
export type Dictionary = Record<string, string>;

export const EN: Dictionary = {
  // shared data table
  'table.columns': 'Columns', 'table.clearFilters': 'Clear filters', 'table.resetLayout': 'Reset layout', 'table.pinStart': 'Pin to start', 'table.pinEnd': 'Pin to end',
  'table.dragToReorder': 'Drag to reorder', 'table.contains': 'Contains…', 'table.any': 'Any', 'table.min': 'Min', 'table.max': 'Max', 'table.reset': 'Reset', 'table.apply': 'Apply',
  'table.pageFilterNote': 'Showing', 'table.onThisPage': 'on this page', 'table.exportCsv': 'Export CSV', 'table.noExport': 'No data to export', 'table.exported': 'CSV exported successfully',
  'table.filter': 'Filter', 'table.region': 'Table', 'table.pinStartShort': 'Start', 'table.pinEndShort': 'End',
  // pagination bar
  'pager.label': 'Pagination', 'pager.showing': 'Showing', 'pager.of': 'of', 'pager.noItems': 'No items', 'pager.first': 'First page', 'pager.prev': 'Previous page',
  'pager.next': 'Next page', 'pager.last': 'Last page', 'pager.prev5': 'Previous 5 pages', 'pager.next5': 'Next 5 pages', 'pager.page': 'Page', 'pager.goTo': 'Go to',
  'pager.goToPage': 'Go to page', 'pager.rowsPerPage': 'Rows per page', 'pager.perPage': '/ page',
  // party statement of account (shared/party-statement.component.ts) and party detail tabs
  'party.statement': 'Statement', 'party.customerStatement': 'Customer statement', 'party.vendorStatement': 'Vendor statement', 'party.documents': 'Documents',
  'statement.refresh': 'Refresh', 'statement.print': 'Print', 'statement.date': 'Date', 'statement.type': 'Type', 'statement.number': 'Number', 'statement.reference': 'Reference',
  'statement.debit': 'Debit', 'statement.credit': 'Credit', 'statement.balance': 'Balance', 'statement.opening': 'Opening balance', 'statement.closing': 'Closing balance',
  'statement.empty': 'No transactions in this period', 'statement.failed': 'Could not load the statement', 'statement.popupBlocked': 'Allow pop-ups for this site to print the statement',
  'statement.customerTitle': 'Customer statement', 'statement.vendorTitle': 'Vendor statement',
  'statement.hintCustomer': 'Positive balance: the customer owes you. Draft and voided documents are not included.',
  'statement.hintVendor': 'Positive balance: you owe the vendor. Draft and voided documents are not included.',
  'statement.kind.invoice': 'Invoice', 'statement.kind.creditNote': 'Credit note', 'statement.kind.receipt': 'Receipt', 'statement.kind.bill': 'Bill', 'statement.kind.payment': 'Payment',
  // legal entity form
  'entity.trn': 'Tax registration no. (TRN)', 'entity.trnHint': 'Printed on tax invoices. Entering a number switches "Tax registered" on.', 'entity.taxRegistered': 'Tax registered',
  'entity.tradeLicence': 'Trade licence / company registration no.', 'entity.tradeLicenceHint': 'The licence or commercial registration number issued by the registering authority.',
};

export const AR: Dictionary = {
  // shared data table
  'table.columns': 'الأعمدة', 'table.clearFilters': 'مسح عوامل التصفية', 'table.resetLayout': 'إعادة ضبط التخطيط', 'table.pinStart': 'تثبيت في البداية', 'table.pinEnd': 'تثبيت في النهاية',
  'table.dragToReorder': 'اسحب لإعادة الترتيب', 'table.contains': 'يحتوي على…', 'table.any': 'الكل', 'table.min': 'الحد الأدنى', 'table.max': 'الحد الأقصى', 'table.reset': 'إعادة تعيين', 'table.apply': 'تطبيق',
  'table.pageFilterNote': 'عرض', 'table.onThisPage': 'في هذه الصفحة', 'table.exportCsv': 'تصدير CSV', 'table.noExport': 'لا توجد بيانات للتصدير', 'table.exported': 'تم تصدير ملف CSV بنجاح',
  'table.filter': 'تصفية', 'table.region': 'جدول', 'table.pinStartShort': 'البداية', 'table.pinEndShort': 'النهاية',
  // pagination bar
  'pager.label': 'ترقيم الصفحات', 'pager.showing': 'عرض', 'pager.of': 'من', 'pager.noItems': 'لا توجد عناصر', 'pager.first': 'الصفحة الأولى', 'pager.prev': 'الصفحة السابقة',
  'pager.next': 'الصفحة التالية', 'pager.last': 'الصفحة الأخيرة', 'pager.prev5': '5 صفحات سابقة', 'pager.next5': '5 صفحات تالية', 'pager.page': 'صفحة', 'pager.goTo': 'انتقال إلى',
  'pager.goToPage': 'الانتقال إلى الصفحة', 'pager.rowsPerPage': 'صفوف لكل صفحة', 'pager.perPage': '/ صفحة',
  // common
  'common.save': 'حفظ', 'common.cancel': 'إلغاء', 'common.new': 'جديد', 'common.search': 'بحث', 'common.run': 'تشغيل', 'common.back': 'رجوع',
  'common.delete': 'حذف', 'common.edit': 'تعديل', 'common.close': 'إغلاق', 'common.yes': 'نعم', 'common.no': 'لا', 'common.total': 'الإجمالي',
  'common.nothing': 'لا يوجد شيء بعد', 'common.upload': 'رفع', 'common.download': 'تنزيل', 'common.status': 'الحالة', 'common.date': 'التاريخ',
  'common.amount': 'المبلغ', 'common.name': 'الاسم', 'common.email': 'البريد الإلكتروني', 'common.password': 'كلمة المرور', 'common.language': 'اللغة',
  // shell
  'shell.signOut': 'تسجيل الخروج', 'shell.notifications': 'الإشعارات', 'shell.markAllRead': 'تحديد الكل كمقروء', 'shell.caughtUp': 'لا توجد إشعارات جديدة',
  'shell.accountSecurity': 'أمان الحساب', 'shell.search': 'ابحث في السجلات', 'shell.darkMode': 'الوضع الداكن', 'shell.allEntities': 'جميع الكيانات القانونية',
  // menu groups and items (server labels are mapped by menu id / group id)
  'menu.group.admin': 'الإدارة', 'menu.group.finance': 'المالية', 'menu.group.realestate': 'العقارات', 'menu.group.hr': 'الموارد البشرية', 'menu.group.inventory': 'المخزون',
  'menu.group.core': 'الأساسيات', 'menu.group.reports': 'التقارير',
  'menu.core.dashboard': 'لوحة التحكم', 'menu.core.parties': 'الأطراف', 'menu.core.items': 'الأصناف', 'menu.core.audit': 'سجل التدقيق', 'menu.core.messages': 'سجل الرسائل',
  'menu.core.documents': 'خزنة المستندات', 'menu.core.import': 'استيراد البيانات', 'menu.core.approvals': 'الموافقات', 'menu.core.users': 'المستخدمون', 'menu.core.roles': 'الأدوار',
  'menu.core.settings': 'الإعدادات', 'menu.core.modules': 'الوحدات', 'menu.finance.journals': 'القيود اليومية', 'menu.finance.budgets': 'الموازنات', 'menu.finance.accounts': 'دليل الحسابات',
  'menu.finance.invoices': 'الفواتير', 'menu.finance.receipts': 'سندات القبض', 'menu.finance.bills': 'فواتير الموردين', 'menu.finance.payments': 'المدفوعات', 'menu.finance.cheques': 'الشيكات',
  'menu.finance.bank': 'البنوك', 'menu.finance.reports': 'التقارير المالية', 'menu.realestate.properties': 'العقارات', 'menu.realestate.units': 'الوحدات', 'menu.realestate.leases': 'عقود الإيجار',
  'menu.realestate.unittypes': 'أنواع الوحدات', 'menu.realestate.acquisitions': 'الاستحواذات', 'menu.realestate.billing': 'فوترة الإيجار', 'menu.realestate.workorders': 'أوامر العمل',
  'menu.realestate.renewals': 'تجديد العقود', 'menu.realestate.pm': 'الصيانة الوقائية', 'menu.realestate.inspections': 'الفحوصات', 'menu.realestate.deals': 'الصفقات',
  'menu.hr.employees': 'الموظفون', 'menu.hr.attendance': 'الحضور', 'menu.hr.payroll': 'الرواتب', 'menu.inventory.warehouses': 'المستودعات', 'menu.inventory.stock': 'المخزون الحالي', 'menu.inventory.movements': 'حركات المخزون',
  // menu groups / items whose server ids had no entry (sidebar showed English in Arabic)
  'menu.group.workspace': 'مساحة العمل', 'menu.group.sales': 'المبيعات', 'menu.group.purchases': 'المشتريات', 'menu.group.masterdata': 'البيانات الأساسية', 'menu.group.setup': 'الإعداد',
  'menu.core.overview': 'نظرة عامة', 'menu.core.reports': 'مركز التقارير', 'menu.core.reports.ar': 'التقارير', 'menu.core.reports.ap': 'التقارير',
  'menu.sales.customers': 'العملاء', 'menu.purchases.vendors': 'الموردون', 'menu.core.approvalrules': 'قواعد الموافقة', 'menu.core.businessunits': 'وحدات الأعمال',
  'menu.core.customfields': 'الحقول المخصصة', 'menu.core.legalentities': 'الكيانات القانونية', 'menu.finance.taxcodes': 'الرموز الضريبية', 'menu.hr.reviewcycles': 'دورات التقييم',
  'menu.hr.reviews': 'تقييمات الأداء', 'menu.realestate.leads': 'العملاء المحتملون', 'menu.realestate.listings': 'القوائم العقارية',
  // login / account
  'login.title': 'تسجيل الدخول', 'login.sub': 'استخدم بريد العمل الخاص بك.', 'login.submit': 'دخول', 'login.forgot': 'نسيت كلمة المرور؟', 'login.remember': 'أبقني مسجلاً',
  'login.code': 'رمز التحقق', 'login.chooseWorkspace': 'اختر مساحة العمل',
  'security.title': 'أمان الحساب', 'security.twoFactor': 'المصادقة الثنائية', 'security.setup': 'إعداد', 'security.turnOn': 'تفعيل', 'security.turnOff': 'إيقاف', 'security.on': 'المصادقة الثنائية مفعّلة.',
  // dashboard
  'dashboard.welcome': 'مرحباً', 'dashboard.overview': 'نظرة عامة', 'dashboard.alerts': 'التنبيهات والمهام',
  // portal
  'portal.title': 'بوابة المستأجر', 'portal.signIn': 'تسجيل الدخول', 'portal.emailCode': 'أرسل لي رمزاً', 'portal.workspace': 'رمز مساحة العمل', 'portal.welcome': 'مرحباً',
  'portal.tab.lease': 'عقدي', 'portal.tab.invoices': 'الفواتير', 'portal.tab.receipts': 'الإيصالات', 'portal.tab.statement': 'كشف الحساب', 'portal.tab.maintenance': 'الصيانة',
  'portal.balance': 'الرصيد المستحق', 'portal.sendRequest': 'إرسال الطلب', 'portal.noLease': 'لا يوجد عقد.', 'portal.signOut': 'تسجيل الخروج',
};

export const DICTIONARIES: Record<Lang, Dictionary> = { en: EN, ar: AR };
export const RTL_LANGS: readonly Lang[] = ['ar'];
