// Fakes for the tap-target audit: a scripted ERP API that answers every call with plausible data, no network.
//
// The app talks to the server only through `ApiClient` (Dio). [FakeErpAdapter] replaces Dio's HTTP adapter, so every
// screen that reads `apiProvider` gets a list page, a record, or an empty success without leaving the test.

import 'dart:convert';
import 'dart:typed_data';

import 'package:dio/dio.dart';

/// Keys the app casts to `num` (`as num?`), plus other count/amount-like keys, so generic records do not throw.
const _numKeys = <String>{
  'absence', 'absentDays', 'achievedMilestonesAmount', 'achievedMilestonesCount', 'activeBomsCount', 'activeRegistersCount',
  'actual', 'actualCash', 'actualCost', 'actualExpense', 'actualHours', 'actualIncome', 'actualNet', 'actualRevenue',
  'actualTotalCost', 'adjustment', 'agentSharePercent', 'allowances', 'amount', 'annualDays', 'annualRatePercent', 'annualRent',
  'annualRentAtRisk', 'area', 'areaSqft', 'askingRent', 'averageCost', 'averageTicket', 'averageYieldRatePercentage', 'balance',
  'basePrice', 'basic', 'basicSalary', 'bedrooms', 'billedMilestonesCount', 'billedQuantity', 'billedTimesheetsCount', 'budget',
  'budgetAmount', 'budgetExpense', 'budgetIncome', 'budgetNet', 'budgetedHours', 'carried', 'carryForwardCap', 'cashDifference',
  'changeAmount', 'closing', 'commissionRatePercent', 'completedQuantity', 'completedTasks', 'completedWorkOrdersMonth',
  'componentCount', 'consumedCost', 'consumedHours', 'contractValue', 'contractedAnnualRent', 'contractedRent', 'costSharePercentage',
  'countedQuantity', 'credit', 'current', 'currentEarnings', 'currentRent', 'damageCost', 'damagedQuantity', 'days', 'days1To30',
  'days31To60', 'days61To90', 'daysLeft', 'daysOverdue', 'daysPresent', 'daysToExpiry', 'daysVacant', 'debit', 'deductions',
  'defaultBedrooms', 'deliveredQuantity', 'discountPercent', 'discountPercentage', 'discountTotal', 'discountValue', 'due', 'draft',
  'employees', 'entitled', 'estimatedCost', 'estimatedHours', 'estimatedTotal', 'estimatedTotalCost', 'estimatedUnitPrice',
  'estimatedValue', 'expectedCash', 'expectedRevenue', 'extendedCost', 'feePercent', 'fiscalYear', 'floor', 'frequencyMonths',
  'graceDays', 'gratuityAccrued', 'grossCommission', 'grossEarnings', 'grossMonthly', 'grossYieldPercent', 'hourlyRate', 'hours',
  'housingAllowance', 'includedHours', 'indicativeValue', 'instalmentCount', 'interest', 'invoiced', 'itemCount', 'laborCost',
  'laborCostPerUnit', 'leases', 'leavePaidDays', 'leaveUnpaidDays', 'lineCount', 'lineTotal', 'loadedQuantity', 'maintenanceCosts',
  'managementFee', 'managementFeePercent', 'marketVariancePercent', 'materialCost', 'maxQuantity', 'minQuantity', 'minimumOrderAmount',
  'month', 'monthlyPayment', 'net', 'netPay', 'netProfit', 'netToOwner', 'noticePeriodDays', 'occupancyPercent', 'occupied',
  'occupiedUnits', 'onHand', 'openSessionsCount', 'openWorkOrdersCount', 'opening', 'openingCash', 'otherAllowances', 'outputQuantity',
  'outstanding', 'over90', 'overageHourlyRate', 'overallRating', 'overheadCost', 'overheadCostPerUnit', 'overtime', 'overtimeHours',
  'paid', 'paidLeaveDays', 'pending', 'plannedHours', 'plannedQuantity', 'price', 'principal', 'probabilityPercent', 'purchasePrice',
  'quantity', 'quantityReceived', 'quantityRequired', 'rate', 'receivedQuantity', 'recurringDeductions', 'recurringFee', 'refund',
  'registered', 'registeredUnits', 'remaining', 'renewed', 'rentCollected', 'reorderLevel', 'repairCost', 'returnedQuantity',
  'salesPrice', 'score', 'scrapPercentage', 'scrappedQuantity', 'securityDepositAmount', 'sequence', 'sequenceOrder', 'serviceYears',
  'soldQuantity', 'stopCount', 'subTotal', 'suggestedOrder', 'suggestedVat', 'systemQuantity', 'taken', 'tax', 'taxAmount', 'taxRate',
  'taxTotal', 'teamLeadSharePercent', 'tendered', 'tenderedAmount', 'termMonths', 'timesUsed', 'todaySales', 'total', 'totalAmount',
  'totalAmountBilled', 'totalAnnualRent', 'totalAssets', 'totalBilled', 'totalBilledHours', 'totalCollectionsAmount', 'totalCost',
  'totalCredit', 'totalDebit', 'totalEquity', 'totalExpenses', 'totalIncome', 'totalLeases', 'totalLiabilities', 'totalLoggedHours',
  'totalManufacturingCostMonth', 'totalMaterialCost', 'totalMilestones', 'totalOrdersToday', 'totalOutstanding', 'totalPayIn',
  'totalPayOut', 'totalProducedQuantityMonth', 'totalRentAtRisk', 'totalReturns', 'totalSales', 'totalSalesAmount',
  'totalStandardUnitCost', 'totalTasks', 'totalTimesheetCost', 'totalUnbilledAmount', 'totalUnitCost', 'totalUnits', 'totalValue',
  'transportAllowance', 'unallocated', 'unallocatedReceipts', 'unbilledHours', 'unbilledHoursAmount', 'unfunded', 'unitCost',
  'unitPrice', 'unitsNotFullyRegistered', 'unpaidAbsentDays', 'unread', 'upliftPercent', 'usageLimit', 'vacancyAskingRent', 'vacant',
  'vacantUnits', 'value', 'variance', 'variancePercent', 'vehicleCapacityKg', 'version', 'withinDays', 'wholesaleUnitPrice',
  'workOrdersCreated', 'workingDays', 'year', 'yearBuilt',
};

/// Keys the app reads as a list of rows.
const _listKeys = <String>{
  'accounts', 'allocations', 'competencies', 'componentBreakdown', 'components', 'drawerTransactions', 'funding', 'instalments',
  'inventoryItems', 'items', 'lines', 'operations', 'payments', 'rows', 'schedule', 'stops', 'assets', 'liabilities', 'equity',
  'income', 'expenses',
};

const _boolKeys = <String>{
  'balanced', 'holdingOverFlag', 'isActive', 'isBank', 'isBillable', 'isBilled', 'isCreditNote', 'isDefault', 'isPaid', 'isPostable',
  'isRecoverable', 'isResolved', 'isSystem', 'pastEnd', 'recorded', 'requiresApproval',
};

const _mapKeys = <String>{'property', 'totals', 'customFields'};

bool _isDateKey(String k) =>
    k.endsWith('Date') || k.endsWith('On') || k.endsWith('At') || k == 'date' || k == 'from' || k == 'to' || k == 'expiry' ||
    k.startsWith('valid') || k.startsWith('period') || k == 'leaseEnds' || k == 'nextDue' || k == 'vacantSince' || k == 'reservedUntil' ||
    k == 'fromDate' || k == 'toDate' || k == 'scheduledFor' || k == 'effectiveFrom';

/// Every key a screen reads, so generic records fill every row and button the real data would.
const _stringKeys = <String>[
  'id', 'number', 'name', 'title', 'code', 'status', 'partyName', 'partyId', 'customerName', 'vendorName', 'employeeName',
  'description', 'memo', 'reference', 'currencyCode', 'currency', 'legalEntityId', 'propertyName', 'unitNumber', 'tenantName',
  'tenant', 'owner', 'unit', 'item', 'sku', 'warehouse', 'itemName', 'itemCode', 'accountCode', 'accountName', 'accountId',
  'kind', 'type', 'method', 'direction', 'stage', 'priority', 'category', 'email', 'phone', 'note', 'notes', 'reason', 'source',
  'sourceType', 'instrumentNo', 'projectName', 'projectNumber', 'clientName', 'department', 'designation', 'jobTitle', 'fullName',
  'employeeNumber', 'leaveTypeName', 'registerName', 'cashierName', 'routeName', 'driverName', 'vehicleNumber', 'tripNumber',
  'workOrderNumber', 'bomCode', 'producedItemName', 'producedItemCode', 'workCenterName', 'operationName', 'subject', 'severity',
  'technician', 'issue', 'propertyType', 'community', 'region', 'unitTypeName', 'usage', 'ownerName', 'leadNumber',
  'opportunityNumber', 'quoteNumber', 'invoiceNumber', 'billNumber', 'vendorReference', 'customerReference', 'paymentTerms',
  'paymentMethod', 'paymentStatus', 'chequeNumber', 'chequeBankName', 'chequeStatus', 'bankName', 'iban', 'taskTitle',
  'milestoneTitle', 'milestoneCode', 'engagementCode', 'retainerNumber', 'requesterName', 'reviewerName', 'cycleName', 'managerName',
  'storeManagerName', 'assignedUserName', 'performedByName', 'inspector', 'condition', 'uom', 'unitOfMeasure', 'group', 'label',
];

/// A record with every field the screens read. Lists hold one nested record (one level deep).
Map<String, dynamic> fakeRecord(int i, {int depth = 0, String status = 'draft'}) {
  final r = <String, dynamic>{};
  for (final k in _stringKeys) {
    r[k] = 'Sample $k $i';
  }
  r['id'] = 'id$i';
  r['number'] = 'DOC-$i';
  r['name'] = 'Record $i';
  r['title'] = 'Record $i';
  r['status'] = status;
  r['currencyCode'] = 'QAR';
  r['currency'] = 'QAR';
  r['legalEntityId'] = 'le1';
  r['month'] = 10;
  r['year'] = 2026;
  r['severity'] = 'warning';
  r['direction'] = 'received';
  r['method'] = 'cash';
  for (final k in _numKeys) {
    r[k] ??= 1000 + i;
  }
  r['month'] = 10;
  r['year'] = 2026;
  for (final k in _boolKeys) {
    r[k] = false;
  }
  for (final k in _mapKeys) {
    r[k] = <String, dynamic>{};
  }
  for (final k in const ['date', 'dueDate', 'startDate', 'endDate', 'invoiceDate', 'billDate', 'from', 'to', 'expiry', 'createdAt']) {
    r[k] = '2026-10-30';
  }
  for (final k in _listKeys) {
    r[k] = depth == 0 ? [fakeRecord(i * 10 + 1, depth: 1)] : <dynamic>[];
  }
  r.removeWhere((k, _) => k.isEmpty);
  return r;
}

/// Fills date-like keys referenced by the screens.
Map<String, dynamic> withDates(Map<String, dynamic> r, Iterable<String> keys) {
  for (final k in keys) {
    if (_isDateKey(k)) r.putIfAbsent(k, () => '2026-10-30');
  }
  return r;
}

/// One page of three records, the shape `Paged.fromJson` reads.
Map<String, dynamic> fakePage({String status = 'draft'}) => {
      'items': [for (var i = 1; i <= 3; i++) fakeRecord(i, status: status)],
      'total': 3,
      'page': 1,
      'pageSize': 25,
    };

ResponseBody _json(Object? data, {int status = 200}) => ResponseBody.fromString(
      jsonEncode({'success': status < 400, 'statusCode': status, 'message': null, 'data': data}),
      status,
      headers: {
        Headers.contentTypeHeader: ['application/json'],
      },
    );

/// Answers every request: list calls (with `page`) get [fakePage]; other GETs get a single [fakeRecord]; writes succeed.
class FakeErpAdapter implements HttpClientAdapter {
  FakeErpAdapter({this.status = 'draft', this.overrides = const {}});

  /// Status of every generated record; 'draft' shows the most actions on detail screens.
  final String status;

  /// Exact answers by path, checked before the generic ones.
  final Map<String, Object? Function(RequestOptions o)> overrides;

  final List<String> requests = [];

  @override
  Future<ResponseBody> fetch(RequestOptions options, Stream<Uint8List>? requestStream, Future<void>? cancelFuture) async {
    requests.add('${options.method} ${options.path}');
    final custom = overrides[options.path];
    if (custom != null) return _json(custom(options));
    if (options.method != 'GET') return _json(<String, dynamic>{});
    if (options.queryParameters.containsKey('page')) return _json(fakePage(status: status));
    final path = options.path;
    // Report and summary endpoints that answer with a bare list.
    if (path.endsWith('/lookup') || path.endsWith('/options')) return _json([fakeRecord(1), fakeRecord(2)]);
    return _json(fakeRecord(1, status: status));
  }

  @override
  void close({bool force = false}) {}
}
