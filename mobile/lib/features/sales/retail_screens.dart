import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../core/format.dart';
import '../../core/me.dart';
import '../../core/session.dart';
import '../../kit/detail_page.dart';
import '../../kit/field.dart';
import '../../kit/form_page.dart';
import '../../kit/list_screen.dart';
import '../../kit/widgets.dart';
import '../common/paged_list_view.dart';

const storesPath = '/api/v1/m/retail/stores';
const priceListsPath = '/api/v1/m/retail/pricelists';
const promotionsPath = '/api/v1/m/retail/promotions';

const priceListTypes = [Option('standard', 'Standard'), Option('promotional', 'Promotional'), Option('tiered', 'Tiered'), Option('member', 'Member')];
const promotionTypes = [Option('percentageDiscount', 'Percentage discount'), Option('fixedAmountDiscount', 'Fixed amount discount'), Option('buyXGetY', 'Buy X get Y'), Option('bundle', 'Bundle')];
const activeFilters = [Option('true', 'Active'), Option('false', 'Inactive')];

Widget _activeChip(Json r) => StatusChip(r['isActive'] == true ? 'Active' : 'Inactive', color: StatusChip.forStatus(r['isActive'] == true ? 'active' : 'inactive'));

Widget _tile(BuildContext context, {required String title, required String subtitle, String? trailing, required Json row, required WidgetBuilder open}) => ListTile(
      onTap: () => Navigator.of(context).push(MaterialPageRoute(builder: open)),
      title: Text(title, maxLines: 1, overflow: TextOverflow.ellipsis),
      subtitle: Text(subtitle),
      isThreeLine: subtitle.contains('\n'),
      trailing: Column(
        mainAxisAlignment: MainAxisAlignment.center,
        crossAxisAlignment: CrossAxisAlignment.end,
        children: [if (trailing != null) Text(trailing, style: const TextStyle(fontWeight: FontWeight.w600)), const SizedBox(height: 4), _activeChip(row)],
      ),
    );


// ---------------------------------------------------------------- stores

FormSpec storeForm(WidgetRef ref, {Json? existing}) {
  final me = ref.read(meOrEmptyProvider);
  final editing = existing != null;
  final entity = me.defaultEntity;
  return FormSpec(
    title: editing ? 'Edit ${existing['code']}' : 'New store',
    initial: {
      'legalEntityId': existing?['legalEntityId'] ?? entity?.id,
      'isActive': existing?['isActive'] ?? true,
      if (editing) ...{
        'name': existing['name'],
        'warehouseId': existing['warehouseId'],
        if (existing['warehouseId'] != null) '__label_warehouseId': 'Selected warehouse',
        'storeManagerName': existing['storeManagerName'],
        'phone': existing['phone'],
        'email': existing['email'],
        'address': existing['address'],
      },
    },
    fields: [
      if (!editing) ...[
        Field.select('legalEntityId', 'Legal entity', options: [for (final e in me.legalEntities) Option(e.id, e.name)], required: true, initial: entity?.id),
        Field.text('code', 'Code', required: true, maxLength: 50),
      ],
      Field.text('name', 'Name', required: true, maxLength: 150),
      Field.lookup('warehouseId', 'Warehouse', path: '/api/v1/m/inventory/warehouses', subtitleKey: 'code'),
      Field.text('storeManagerName', 'Store manager', maxLength: 150),
      Field.text('phone', 'Phone', maxLength: 50, keyboard: TextInputType.phone),
      Field.text('email', 'Email', maxLength: 100, keyboard: TextInputType.emailAddress),
      Field.multiline('address', 'Address', maxLength: 500),
      Field.toggle('isActive', 'Active', initial: true),
    ],
    submit: (ref, body, raw) {
      final api = ref.read(apiProvider);
      return editing ? api.put('$storesPath/${existing['id']}', body: body) : api.post(storesPath, body: body);
    },
  );
}

Widget storeTile(BuildContext context, Json s) => _tile(context,
    title: '${s['code']} · ${s['name']}',
    subtitle: [s['storeManagerName'], s['phone']].where((e) => e != null && e.toString().isNotEmpty).join(' · '),
    row: s,
    open: (_) => StoreDetailScreen(id: s['id'].toString()));

class StoresScreen extends StatelessWidget {
  const StoresScreen({super.key});

  @override
  Widget build(BuildContext context) => ListScreen(
        title: 'Stores',
        path: storesPath,
        searchHint: 'Search stores',
        emptyText: 'No stores found.',
        itemBuilder: storeTile,
        filters: activeFilters,
        filterKey: 'isActive',
        create: (ref) => storeForm(ref),
        createPermission: 'retail.stores.manage',
      );
}

class StoreDetailScreen extends StatelessWidget {
  const StoreDetailScreen({super.key, required this.id});

  final String id;

  @override
  Widget build(BuildContext context) => Consumer(builder: (context, ref, _) {
        return DetailPage(
          path: '$storesPath/$id',
          titleOf: (d) => '${d['code']}',
          canEdit: (_) => ref.read(meOrEmptyProvider).can('retail.stores.manage'),
          edit: (d) => storeForm(ref, existing: d),
          body: (context, d) => DetailBody(children: [
            DetailHeader(title: '${d['name']}', subtitle: d['code']?.toString()),
            _activeChip(d),
            const SizedBox(height: 12),
            InfoCard(rows: [
              InfoRow('Manager', d['storeManagerName']?.toString()),
              InfoRow('Phone', d['phone']?.toString()),
              InfoRow('Email', d['email']?.toString()),
              InfoRow('Address', d['address']?.toString()),
            ]),
          ]),
          actions: [
            ActionSpec(
              label: 'Delete',
              icon: Icons.delete_outline,
              permission: 'retail.stores.manage',
              destructive: true,
              confirm: 'Delete this store?',
              successMessage: 'Deleted',
              run: (ref, d, _) async {
                await ref.read(apiProvider).delete('$storesPath/$id');
                if (context.mounted) Navigator.of(context).pop();
                return null;
              },
            ),
          ],
        );
      });
}

// ----------------------------------------------------------- price lists

FormSpec priceListForm(WidgetRef ref, {Json? existing}) {
  final me = ref.read(meOrEmptyProvider);
  final editing = existing != null;
  final entity = me.defaultEntity;
  return FormSpec(
    title: editing ? 'Edit ${existing['code']}' : 'New price list',
    initial: {
      'legalEntityId': existing?['legalEntityId'] ?? entity?.id,
      'currency': existing?['currency'] ?? entity?.baseCurrency,
      'type': existing?['type'] ?? 'standard',
      'isActive': existing?['isActive'] ?? true,
      'isDefault': existing?['isDefault'] ?? false,
      if (editing) ...{
        'name': existing['name'],
        'validFrom': existing['validFrom'],
        'validTo': existing['validTo'],
        'description': existing['description'],
        'items': [
          for (final i in (existing['items'] as List<dynamic>? ?? const []))
            {
              'itemId': (i as Json)['itemId'],
              if (i['itemId'] != null) '__label_itemId': i['itemName'] ?? i['itemCode'],
              'itemCode': i['itemCode'],
              'itemName': i['itemName'],
              'basePrice': i['basePrice'],
              'price': i['price'],
              'minQuantity': i['minQuantity'],
            },
        ],
      },
    },
    fields: [
      if (!editing) ...[
        Field.select('legalEntityId', 'Legal entity', options: [for (final e in me.legalEntities) Option(e.id, e.name)], required: true, initial: entity?.id),
        Field.text('code', 'Code', required: true, maxLength: 50),
      ],
      Field.text('name', 'Name', required: true, maxLength: 150),
      Field.select('type', 'Type', options: priceListTypes, required: true, initial: 'standard'),
      Field.text('currency', 'Currency', required: true, maxLength: 10),
      Field.date('validFrom', 'Valid from'),
      Field.date('validTo', 'Valid to', validator: (v, all) => v != null && all['validFrom'] != null && v.toString().compareTo(all['validFrom'].toString()) < 0 ? 'Must not be before the start date' : null),
      Field.toggle('isDefault', 'Default list'),
      Field.toggle('isActive', 'Active', initial: true),
      Field.multiline('description', 'Description', maxLength: 500),
      Field.lines('items', 'Items', minLines: 0, addLabel: 'Add item', fields: [
        Field.lookup('itemId', 'Item (optional)', path: '/api/v1/items', subtitleKey: 'sku', copy: const {'itemCode': 'sku', 'itemName': 'name', 'basePrice': 'salesPrice', 'price': 'salesPrice'}),
        Field.text('itemCode', 'Item code', required: true, maxLength: 50),
        Field.text('itemName', 'Item name', required: true, maxLength: 200),
        Field.money('basePrice', 'Base price', required: true),
        Field.money('price', 'Price', required: true),
        Field.decimal('minQuantity', 'Minimum quantity', min: 0),
      ]),
    ],
    submit: (ref, body, raw) {
      final api = ref.read(apiProvider);
      final b = {...body, 'items': body['items'] ?? const []};
      return editing ? api.put('$priceListsPath/${existing['id']}', body: b) : api.post(priceListsPath, body: b);
    },
  );
}

Widget priceListTile(BuildContext context, Json p) => _tile(context,
    title: '${p['code']} · ${p['name']}',
    subtitle: '${humanize(p['type']?.toString())} · ${p['currency']}\n${p['itemCount'] ?? 0} items · from ${formatDate(p['validFrom'])}',
    row: p,
    open: (_) => PriceListDetailScreen(id: p['id'].toString()));

class PriceListsScreen extends StatelessWidget {
  const PriceListsScreen({super.key});

  @override
  Widget build(BuildContext context) => ListScreen(
        title: 'Price lists',
        path: priceListsPath,
        searchHint: 'Search price lists',
        emptyText: 'No price lists found.',
        itemBuilder: priceListTile,
        filters: priceListTypes,
        filterKey: 'type',
        create: (ref) => priceListForm(ref),
        createPermission: 'retail.pricelists.manage',
      );
}

class PriceListDetailScreen extends StatelessWidget {
  const PriceListDetailScreen({super.key, required this.id});

  final String id;

  @override
  Widget build(BuildContext context) => Consumer(builder: (context, ref, _) {
        return DetailPage(
          path: '$priceListsPath/$id',
          titleOf: (d) => '${d['code']}',
          canEdit: (_) => ref.read(meOrEmptyProvider).can('retail.pricelists.manage'),
          edit: (d) => priceListForm(ref, existing: d),
          body: (context, d) {
            final items = (d['items'] as List<dynamic>? ?? const []).cast<Json>();
            return DetailBody(children: [
              DetailHeader(title: '${d['name']}', subtitle: '${humanize(d['type']?.toString())} · ${d['currency']}'),
              _activeChip(d),
              const SizedBox(height: 12),
              InfoCard(rows: [
                InfoRow('Valid from', formatDate(d['validFrom'])),
                InfoRow('Valid to', d['validTo'] == null ? null : formatDate(d['validTo'])),
                InfoRow('Default list', d['isDefault'] == true ? 'Yes' : 'No'),
                InfoRow('Description', d['description']?.toString()),
              ]),
              Text('Items (${items.length})', style: Theme.of(context).textTheme.titleMedium),
              for (final i in items)
                ListTile(
                  contentPadding: EdgeInsets.zero,
                  title: Text('${i['itemName']}'),
                  subtitle: Text('${i['itemCode']} · base ${formatMoney((i['basePrice'] as num?) ?? 0)}${i['minQuantity'] == null ? '' : ' · min ${formatMoney(i['minQuantity'] as num)}'}'),
                  trailing: Text(formatMoney((i['price'] as num?) ?? 0), style: const TextStyle(fontWeight: FontWeight.w600)),
                ),
            ]);
          },
          actions: [
            ActionSpec(
              label: 'Delete',
              icon: Icons.delete_outline,
              permission: 'retail.pricelists.manage',
              destructive: true,
              confirm: 'Delete this price list?',
              successMessage: 'Deleted',
              run: (ref, d, _) async {
                await ref.read(apiProvider).delete('$priceListsPath/$id');
                if (context.mounted) Navigator.of(context).pop();
                return null;
              },
            ),
          ],
        );
      });
}

// ------------------------------------------------------------ promotions

FormSpec promotionForm(WidgetRef ref, {Json? existing}) {
  final me = ref.read(meOrEmptyProvider);
  final editing = existing != null;
  final entity = me.defaultEntity;
  return FormSpec(
    title: editing ? 'Edit ${existing['code']}' : 'New promotion',
    initial: {
      'legalEntityId': existing?['legalEntityId'] ?? entity?.id,
      'type': existing?['type'] ?? 'percentageDiscount',
      'isActive': existing?['isActive'] ?? true,
      if (editing) ...{
        'name': existing['name'],
        'discountValue': existing['discountValue'],
        'minimumOrderAmount': existing['minimumOrderAmount'],
        'startDate': existing['startDate'],
        'endDate': existing['endDate'],
        'usageLimit': existing['usageLimit'],
        'description': existing['description'],
      },
    },
    fields: [
      if (!editing) ...[
        Field.select('legalEntityId', 'Legal entity', options: [for (final e in me.legalEntities) Option(e.id, e.name)], required: true, initial: entity?.id),
        Field.text('code', 'Promo code', required: true, maxLength: 50),
      ],
      Field.text('name', 'Name', required: true, maxLength: 150),
      Field.select('type', 'Type', options: promotionTypes, required: true, initial: 'percentageDiscount'),
      Field.money('discountValue', 'Discount value', required: true, min: 0.01),
      Field.money('minimumOrderAmount', 'Minimum order amount'),
      Field.date('startDate', 'Start date', required: true, initial: 'today'),
      Field.date('endDate', 'End date', required: true, validator: (v, all) => v != null && all['startDate'] != null && v.toString().compareTo(all['startDate'].toString()) < 0 ? 'Must not be before the start date' : null),
      Field.integer('usageLimit', 'Usage limit', min: 1),
      Field.toggle('isActive', 'Active', initial: true),
      Field.multiline('description', 'Description', maxLength: 500),
    ],
    submit: (ref, body, raw) {
      final api = ref.read(apiProvider);
      return editing ? api.put('$promotionsPath/${existing['id']}', body: body) : api.post(promotionsPath, body: body);
    },
  );
}

String promotionValue(Json p) => p['type'] == 'percentageDiscount' ? '${formatMoney((p['discountValue'] as num?) ?? 0)}%' : formatMoney((p['discountValue'] as num?) ?? 0);

Widget promotionTile(BuildContext context, Json p) => _tile(context,
    title: '${p['code']} · ${p['name']}',
    subtitle: '${humanize(p['type']?.toString())}\n${formatDate(p['startDate'])} to ${formatDate(p['endDate'])}',
    trailing: promotionValue(p),
    row: p,
    open: (_) => PromotionDetailScreen(id: p['id'].toString()));

class PromotionsScreen extends StatelessWidget {
  const PromotionsScreen({super.key});

  @override
  Widget build(BuildContext context) => ListScreen(
        title: 'Promotions',
        path: promotionsPath,
        searchHint: 'Search promotions',
        emptyText: 'No promotions found.',
        itemBuilder: promotionTile,
        filters: activeFilters,
        filterKey: 'isActive',
        create: (ref) => promotionForm(ref),
        createPermission: 'retail.promotions.manage',
      );
}

class PromotionDetailScreen extends StatelessWidget {
  const PromotionDetailScreen({super.key, required this.id});

  final String id;

  Future<Object?> _setActive(WidgetRef ref, Json d, bool active) => ref.read(apiProvider).put('$promotionsPath/$id', body: {
        'name': d['name'],
        'type': d['type'],
        'discountValue': d['discountValue'],
        if (d['minimumOrderAmount'] != null) 'minimumOrderAmount': d['minimumOrderAmount'],
        'startDate': d['startDate'],
        'endDate': d['endDate'],
        if (d['usageLimit'] != null) 'usageLimit': d['usageLimit'],
        'isActive': active,
        if (d['description'] != null) 'description': d['description'],
      });

  @override
  Widget build(BuildContext context) => Consumer(builder: (context, ref, _) {
        return DetailPage(
          path: '$promotionsPath/$id',
          titleOf: (d) => '${d['code']}',
          canEdit: (_) => ref.read(meOrEmptyProvider).can('retail.promotions.manage'),
          edit: (d) => promotionForm(ref, existing: d),
          body: (context, d) => DetailBody(children: [
            DetailHeader(title: '${d['name']}', subtitle: humanize(d['type']?.toString())),
            _activeChip(d),
            const SizedBox(height: 12),
            InfoCard(rows: [
              InfoRow('Discount', promotionValue(d), bold: true),
              InfoRow('Minimum order', d['minimumOrderAmount'] == null ? null : formatMoney(d['minimumOrderAmount'] as num)),
              InfoRow('Runs', '${formatDate(d['startDate'])} to ${formatDate(d['endDate'])}'),
              InfoRow('Used', '${d['timesUsed'] ?? 0}${d['usageLimit'] == null ? '' : ' of ${d['usageLimit']}'}'),
              InfoRow('Description', d['description']?.toString()),
            ]),
          ]),
          actions: [
            ActionSpec(
              label: 'Deactivate',
              icon: Icons.pause_circle_outline,
              permission: 'retail.promotions.manage',
              visible: (d) => d['isActive'] == true,
              confirm: 'Stop this promotion?',
              successMessage: 'Deactivated',
              run: (ref, d, _) => _setActive(ref, d, false),
            ),
            ActionSpec(
              label: 'Activate',
              icon: Icons.play_circle_outline,
              permission: 'retail.promotions.manage',
              visible: (d) => d['isActive'] != true,
              successMessage: 'Activated',
              run: (ref, d, _) => _setActive(ref, d, true),
            ),
            ActionSpec(
              label: 'Delete',
              icon: Icons.delete_outline,
              permission: 'retail.promotions.manage',
              destructive: true,
              confirm: 'Delete this promotion?',
              successMessage: 'Deleted',
              run: (ref, d, _) async {
                await ref.read(apiProvider).delete('$promotionsPath/$id');
                if (context.mounted) Navigator.of(context).pop();
                return null;
              },
            ),
          ],
        );
      });
}
