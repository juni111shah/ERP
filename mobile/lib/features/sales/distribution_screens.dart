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

const routesPath = '/api/v1/m/distribution/routes';
const tripsPath = '/api/v1/m/distribution/trips';
const wholesalePath = '/api/v1/m/distribution/wholesale';

const wholesaleTiers = [Option('standard', 'Standard'), Option('bronze', 'Bronze'), Option('silver', 'Silver'), Option('gold', 'Gold'), Option('platinum', 'Platinum')];

Widget _activeChip(Json r) => StatusChip(r['isActive'] == true ? 'Active' : 'Inactive', color: StatusChip.forStatus(r['isActive'] == true ? 'active' : 'inactive'));


String _qty(Object? v) => formatMoney((v as num?) ?? 0);

// ---------------------------------------------------------------- routes

FormSpec routeForm(WidgetRef ref, {Json? existing}) {
  final me = ref.read(meOrEmptyProvider);
  final editing = existing != null;
  final entity = me.defaultEntity;
  return FormSpec(
    title: editing ? 'Edit ${existing['code']}' : 'New route',
    initial: {
      'legalEntityId': existing?['legalEntityId'] ?? entity?.id,
      'isActive': existing?['isActive'] ?? true,
      if (editing) ...{
        'name': existing['name'],
        'description': existing['description'],
        'driverName': existing['driverName'],
        'vehicleNumber': existing['vehicleNumber'],
        'vehicleCapacityKg': existing['vehicleCapacityKg'],
        'warehouseId': existing['warehouseId'],
        if (existing['warehouseId'] != null) '__label_warehouseId': 'Selected warehouse',
        'stops': [
          for (final s in (existing['stops'] as List<dynamic>? ?? const []))
            {
              'customerPartyId': (s as Json)['customerPartyId'],
              '__label_customerPartyId': s['customerName'],
              'customerName': s['customerName'],
              'sequenceOrder': s['sequenceOrder'],
              'address': s['address'],
              'contactPhone': s['contactPhone'],
              'preferredTimeWindow': s['preferredTimeWindow'],
              'specialInstructions': s['specialInstructions'],
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
      Field.multiline('description', 'Description', maxLength: 500),
      Field.text('driverName', 'Driver', maxLength: 150),
      Field.text('vehicleNumber', 'Vehicle number', maxLength: 50),
      Field.decimal('vehicleCapacityKg', 'Vehicle capacity (kg)', min: 0),
      Field.lookup('warehouseId', 'Warehouse', path: '/api/v1/m/inventory/warehouses', subtitleKey: 'code'),
      Field.toggle('isActive', 'Active', initial: true),
      Field.lines('stops', 'Delivery stops', minLines: 0, addLabel: 'Add stop', fields: [
        Field.lookup('customerPartyId', 'Customer', path: '/api/v1/parties', query: const {'role': 'Customer'}, subtitleKey: 'code', required: true, copy: const {'customerName': 'name', 'address': 'address', 'contactPhone': 'phone'}),
        Field.text('customerName', 'Customer name', required: true, maxLength: 200),
        Field.integer('sequenceOrder', 'Stop order', required: true, min: 1, initial: '1'),
        Field.text('address', 'Address', maxLength: 500),
        Field.text('contactPhone', 'Contact phone', maxLength: 50, keyboard: TextInputType.phone),
        Field.text('preferredTimeWindow', 'Preferred time window', maxLength: 100),
        Field.text('specialInstructions', 'Special instructions', maxLength: 500),
      ]),
    ],
    submit: (ref, body, raw) {
      final api = ref.read(apiProvider);
      final b = {...body, 'stops': body['stops'] ?? const [], if (editing && existing['assignedDriverUserId'] != null) 'assignedDriverUserId': existing['assignedDriverUserId']};
      return editing ? api.put('$routesPath/${existing['id']}', body: b) : api.post(routesPath, body: b);
    },
  );
}

Widget routeTile(BuildContext context, Json r) => ListTile(
      onTap: () => Navigator.of(context).push(MaterialPageRoute(builder: (_) => RouteDetailScreen(id: r['id'].toString()))),
      title: Text('${r['code']} · ${r['name']}', maxLines: 1, overflow: TextOverflow.ellipsis),
      subtitle: Text('${[r['driverName'], r['vehicleNumber']].where((e) => e != null && e.toString().isNotEmpty).join(' · ')}\n${r['stopCount'] ?? 0} stops'),
      isThreeLine: true,
      trailing: _activeChip(r),
    );

class RoutesScreen extends StatelessWidget {
  const RoutesScreen({super.key});

  @override
  Widget build(BuildContext context) => ListScreen(
        title: 'Routes',
        path: routesPath,
        searchHint: 'Search routes',
        emptyText: 'No routes found.',
        itemBuilder: routeTile,
        create: (ref) => routeForm(ref),
        createPermission: 'distribution.routes.manage',
      );
}

class RouteDetailScreen extends StatelessWidget {
  const RouteDetailScreen({super.key, required this.id});

  final String id;

  @override
  Widget build(BuildContext context) => Consumer(builder: (context, ref, _) {
        return DetailPage(
          path: '$routesPath/$id',
          titleOf: (d) => '${d['code']}',
          canEdit: (_) => ref.read(meOrEmptyProvider).can('distribution.routes.manage'),
          edit: (d) => routeForm(ref, existing: d),
          body: (context, d) {
            final stops = (d['stops'] as List<dynamic>? ?? const []).cast<Json>();
            return DetailBody(children: [
              DetailHeader(title: '${d['name']}', subtitle: d['description']?.toString()),
              _activeChip(d),
              const SizedBox(height: 12),
              InfoCard(rows: [
                InfoRow('Driver', d['driverName']?.toString()),
                InfoRow('Vehicle', d['vehicleNumber']?.toString()),
                InfoRow('Capacity', d['vehicleCapacityKg'] == null ? null : '${_qty(d['vehicleCapacityKg'])} kg'),
              ]),
              Text('Stops (${stops.length})', style: Theme.of(context).textTheme.titleMedium),
              for (final s in stops)
                ListTile(
                  contentPadding: EdgeInsets.zero,
                  leading: CircleAvatar(radius: 14, child: Text('${s['sequenceOrder']}')),
                  title: Text('${s['customerName']}'),
                  subtitle: Text([s['address'], s['preferredTimeWindow'], s['contactPhone'], s['specialInstructions']].where((e) => e != null && e.toString().isNotEmpty).join('\n')),
                ),
            ]);
          },
          actions: [
            ActionSpec(
              label: 'Delete',
              icon: Icons.delete_outline,
              permission: 'distribution.routes.manage',
              destructive: true,
              confirm: 'Delete this route and its stops?',
              successMessage: 'Deleted',
              run: (ref, d, _) async {
                await ref.read(apiProvider).delete('$routesPath/$id');
                if (context.mounted) Navigator.of(context).pop();
                return null;
              },
            ),
          ],
        );
      });
}

// ----------------------------------------------------------------- trips

FormSpec tripForm(WidgetRef ref) {
  final me = ref.read(meOrEmptyProvider);
  final entity = me.defaultEntity;
  return FormSpec(
    title: 'New van sales trip',
    intro: 'The trip starts right away; complete it when the van is back.',
    initial: {'legalEntityId': entity?.id},
    fields: [
      Field.select('legalEntityId', 'Legal entity', options: [for (final e in me.legalEntities) Option(e.id, e.name)], required: true, initial: entity?.id),
      Field.lookup('routeId', 'Route', path: routesPath, subtitleKey: 'code', required: true, copy: const {'driverName': 'driverName', 'vehicleNumber': 'vehicleNumber'}),
      Field.lookup('driverUserId', 'Driver (user)', path: '/api/v1/users', labelKey: 'fullName', subtitleKey: 'email', required: true),
      Field.text('driverName', 'Driver name', required: true, maxLength: 150),
      Field.text('vehicleNumber', 'Vehicle number', required: true, maxLength: 50),
      Field.lookup('warehouseId', 'Load from warehouse', path: '/api/v1/m/inventory/warehouses', subtitleKey: 'code', required: true),
      Field.date('departureDate', 'Departure date', required: true, initial: 'today'),
      Field.multiline('notes', 'Notes', maxLength: 1000),
      Field.lines('items', 'Stock loaded on the van', addLabel: 'Add item', fields: [
        Field.lookup('itemId', 'Item', path: '/api/v1/items', subtitleKey: 'sku', required: true, copy: const {'itemCode': 'sku', 'itemName': 'name', 'unitPrice': 'salesPrice'}),
        Field.decimal('loadedQuantity', 'Quantity loaded', required: true, min: 0.0001),
        Field.text('uom', 'Unit', maxLength: 20, initial: 'PCS'),
        Field.money('unitPrice', 'Unit price', required: true),
      ]),
    ],
    submit: (ref, body, raw) {
      final items = [
        for (final i in (body['items'] as List<dynamic>))
          {...(i as Map<String, dynamic>), 'itemCode': i['itemCode'] ?? '', 'itemName': i['itemName'] ?? '', 'uom': i['uom'] ?? 'PCS'},
      ];
      return ref.read(apiProvider).post(tripsPath, body: {...body, 'items': items});
    },
  );
}

/// Closing a trip: totals plus sold / returned / damaged per loaded item.
FormSpec completeTripForm(Json trip) {
  final items = (trip['inventoryItems'] as List<dynamic>? ?? const []).cast<Json>();
  return FormSpec(
    title: 'Complete ${trip['tripNumber']}',
    submitLabel: 'Complete trip',
    initial: {
      'totalSalesAmount': trip['totalSalesAmount'],
      'totalCollectionsAmount': trip['totalCollectionsAmount'],
      for (var i = 0; i < items.length; i++) ...{'sold_$i': '0', 'returned_$i': '0', 'damaged_$i': '0'},
    },
    fields: [
      Field.money('totalSalesAmount', 'Total sales', required: true),
      Field.money('totalCollectionsAmount', 'Total collected', required: true),
      for (var i = 0; i < items.length; i++) ...[
        Field.decimal('sold_$i', '${items[i]['itemName']}: sold (loaded ${_qty(items[i]['loadedQuantity'])})', required: true, min: 0),
        Field.decimal('returned_$i', '${items[i]['itemName']}: returned', required: true, min: 0),
        Field.decimal('damaged_$i', '${items[i]['itemName']}: damaged', required: true, min: 0,
            validator: (v, all) {
              final loaded = (items[i]['loadedQuantity'] as num?) ?? 0;
              final sum = (toNum(all['sold_$i']) ?? 0) + (toNum(all['returned_$i']) ?? 0) + (toNum(v) ?? 0);
              return sum > loaded ? 'Sold, returned and damaged exceed the ${_qty(loaded)} loaded' : null;
            }),
      ],
      Field.multiline('notes', 'Reconciliation notes', maxLength: 1000),
    ],
    submit: (ref, body, raw) => ref.read(apiProvider).post('$tripsPath/${trip['id']}/complete', body: {
          'totalSalesAmount': body['totalSalesAmount'],
          'totalCollectionsAmount': body['totalCollectionsAmount'],
          'items': [
            for (var i = 0; i < items.length; i++)
              {'itemId': items[i]['itemId'], 'soldQuantity': body['sold_$i'], 'returnedQuantity': body['returned_$i'], 'damagedQuantity': body['damaged_$i']},
          ],
          if (body['notes'] != null) 'notes': body['notes'],
        }),
  );
}

Widget tripTile(BuildContext context, Json t) {
  final status = (t['status'] ?? '').toString();
  return ListTile(
    onTap: () => Navigator.of(context).push(MaterialPageRoute(builder: (_) => TripDetailScreen(id: t['id'].toString()))),
    title: Text('${t['tripNumber']}', maxLines: 1, overflow: TextOverflow.ellipsis),
    subtitle: Text('${t['routeName'] ?? ''}\n${t['driverName'] ?? ''} · ${formatDate(t['departureDate'])}'),
    isThreeLine: true,
    trailing: Column(
      mainAxisAlignment: MainAxisAlignment.center,
      crossAxisAlignment: CrossAxisAlignment.end,
      children: [
        Text(formatMoney((t['totalSalesAmount'] as num?) ?? 0), style: const TextStyle(fontWeight: FontWeight.w600)),
        const SizedBox(height: 4),
        StatusChip(humanize(status), color: StatusChip.forStatus(status)),
      ],
    ),
  );
}

class TripsScreen extends StatelessWidget {
  const TripsScreen({super.key});

  @override
  Widget build(BuildContext context) => ListScreen(
        title: 'Van sales',
        path: tripsPath,
        searchHint: 'Search trips',
        emptyText: 'No trips found.',
        itemBuilder: tripTile,
        create: (ref) => tripForm(ref),
        createPermission: 'distribution.trips.manage',
      );
}

class TripDetailScreen extends StatelessWidget {
  const TripDetailScreen({super.key, required this.id});

  final String id;

  @override
  Widget build(BuildContext context) => DetailPage(
        path: '$tripsPath/$id',
        titleOf: (d) => '${d['tripNumber']}',
        body: (context, d) {
          final items = (d['inventoryItems'] as List<dynamic>? ?? const []).cast<Json>();
          return DetailBody(children: [
            DetailHeader(title: '${d['routeName']}', subtitle: '${d['driverName']} · ${d['vehicleNumber']}', status: d['status']?.toString()),
            InfoCard(rows: [
              InfoRow('Departure', formatDate(d['departureDate'])),
              InfoRow('Returned', d['returnDate'] == null ? null : formatDate(d['returnDate'])),
              InfoRow('Sales', formatMoney((d['totalSalesAmount'] as num?) ?? 0), bold: true),
              InfoRow('Collected', formatMoney((d['totalCollectionsAmount'] as num?) ?? 0), bold: true),
              InfoRow('Notes', d['notes']?.toString()),
            ]),
            Text('Van stock (${items.length})', style: Theme.of(context).textTheme.titleMedium),
            for (final i in items)
              ListTile(
                contentPadding: EdgeInsets.zero,
                title: Text('${i['itemName']}'),
                subtitle: Text('${i['itemCode']} · ${_qty(i['unitPrice'])} per ${i['uom']}\nLoaded ${_qty(i['loadedQuantity'])} · sold ${_qty(i['soldQuantity'])} · returned ${_qty(i['returnedQuantity'])} · damaged ${_qty(i['damagedQuantity'])}'),
                isThreeLine: true,
              ),
          ]);
        },
        actions: [
          ActionSpec(
            label: 'Complete trip',
            icon: Icons.flag_outlined,
            permission: 'distribution.trips.manage',
            visible: (d) => d['status'] == 'scheduled' || d['status'] == 'inProgress',
            form: completeTripForm,
            run: (ref, d, _) async => null,
          ),
        ],
      );
}

// ------------------------------------------------------------- wholesale

FormSpec wholesaleForm(WidgetRef ref, {Json? existing}) {
  final me = ref.read(meOrEmptyProvider);
  final editing = existing != null;
  final entity = me.defaultEntity;
  return FormSpec(
    title: editing ? 'Edit ${existing['itemCode']}' : 'New wholesale price',
    initial: {
      'legalEntityId': existing?['legalEntityId'] ?? entity?.id,
      'tier': existing?['tier'] ?? 'standard',
      'isActive': existing?['isActive'] ?? true,
      'discountPercentage': existing?['discountPercentage'] ?? '0',
      if (editing) ...{
        'minQuantity': existing['minQuantity'],
        'maxQuantity': existing['maxQuantity'],
        'wholesaleUnitPrice': existing['wholesaleUnitPrice'],
        'notes': existing['notes'],
      },
    },
    fields: [
      if (!editing) ...[
        Field.select('legalEntityId', 'Legal entity', options: [for (final e in me.legalEntities) Option(e.id, e.name)], required: true, initial: entity?.id),
        Field.lookup('itemId', 'Item', path: '/api/v1/items', subtitleKey: 'sku', required: true, copy: const {'itemCode': 'sku', 'itemName': 'name', 'wholesaleUnitPrice': 'salesPrice'}),
        Field.text('itemCode', 'Item code', required: true, maxLength: 50),
        Field.text('itemName', 'Item name', required: true, maxLength: 200),
      ],
      Field.select('tier', 'Tier', options: wholesaleTiers, required: true, initial: 'standard'),
      Field.decimal('minQuantity', 'Minimum quantity', required: true, min: 0.0001),
      Field.decimal('maxQuantity', 'Maximum quantity', min: 0.0001),
      Field.decimal('discountPercentage', 'Discount %', required: true, min: 0, max: 100, decimals: 2, initial: '0'),
      Field.money('wholesaleUnitPrice', 'Wholesale unit price', required: true, min: 0.01),
      Field.toggle('isActive', 'Active', initial: true),
      Field.multiline('notes', 'Notes', maxLength: 500),
    ],
    submit: (ref, body, raw) {
      final api = ref.read(apiProvider);
      return editing ? api.put('$wholesalePath/${existing['id']}', body: body) : api.post(wholesalePath, body: body);
    },
  );
}

Widget wholesaleTile(BuildContext context, Json w) => ListTile(
      onTap: () => Navigator.of(context).push(MaterialPageRoute(builder: (_) => WholesaleDetailScreen(id: w['id'].toString()))),
      title: Text('${w['itemName']}', maxLines: 1, overflow: TextOverflow.ellipsis),
      subtitle: Text('${w['itemCode']} · ${humanize(w['tier']?.toString())}\nFrom ${_qty(w['minQuantity'])} units · ${_qty(w['discountPercentage'])}% off'),
      isThreeLine: true,
      trailing: Column(
        mainAxisAlignment: MainAxisAlignment.center,
        crossAxisAlignment: CrossAxisAlignment.end,
        children: [Text(formatMoney((w['wholesaleUnitPrice'] as num?) ?? 0), style: const TextStyle(fontWeight: FontWeight.w600)), const SizedBox(height: 4), _activeChip(w)],
      ),
    );

class WholesaleScreen extends StatelessWidget {
  const WholesaleScreen({super.key});

  @override
  Widget build(BuildContext context) => ListScreen(
        title: 'Wholesale pricing',
        path: wholesalePath,
        searchHint: 'Search wholesale prices',
        emptyText: 'No wholesale prices found.',
        itemBuilder: wholesaleTile,
        create: (ref) => wholesaleForm(ref),
        createPermission: 'distribution.pricing.manage',
      );
}

class WholesaleDetailScreen extends StatelessWidget {
  const WholesaleDetailScreen({super.key, required this.id});

  final String id;

  @override
  Widget build(BuildContext context) => Consumer(builder: (context, ref, _) {
        return DetailPage(
          path: '$wholesalePath/$id',
          titleOf: (d) => '${d['itemCode']}',
          canEdit: (_) => ref.read(meOrEmptyProvider).can('distribution.pricing.manage'),
          edit: (d) => wholesaleForm(ref, existing: d),
          body: (context, d) => DetailBody(children: [
            DetailHeader(title: '${d['itemName']}', subtitle: '${d['itemCode']} · ${humanize(d['tier']?.toString())}'),
            _activeChip(d),
            const SizedBox(height: 12),
            InfoCard(rows: [
              InfoRow('Unit price', formatMoney((d['wholesaleUnitPrice'] as num?) ?? 0), bold: true),
              InfoRow('Discount', '${_qty(d['discountPercentage'])}%'),
              InfoRow('Minimum quantity', _qty(d['minQuantity'])),
              InfoRow('Maximum quantity', d['maxQuantity'] == null ? null : _qty(d['maxQuantity'])),
              InfoRow('Notes', d['notes']?.toString()),
            ]),
          ]),
          actions: [
            ActionSpec(
              label: 'Delete',
              icon: Icons.delete_outline,
              permission: 'distribution.pricing.manage',
              destructive: true,
              confirm: 'Delete this price?',
              successMessage: 'Deleted',
              run: (ref, d, _) async {
                await ref.read(apiProvider).delete('$wholesalePath/$id');
                if (context.mounted) Navigator.of(context).pop();
                return null;
              },
            ),
          ],
        );
      });
}
