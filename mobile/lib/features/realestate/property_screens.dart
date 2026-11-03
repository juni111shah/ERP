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
import 'common.dart';
import 'lease_screens.dart';
import 'maintenance_screens.dart';
import 'report_screens.dart';

const _propertiesPath = '$rePath/properties';
const _unitsPath = '$rePath/units';
const _unitTypesPath = '$rePath/unit-types';

const propertyTypes = [
  Option('residential', 'Residential'),
  Option('villa', 'Villa'),
  Option('commercial', 'Commercial'),
  Option('mixedUse', 'Mixed use'),
  Option('retail', 'Retail'),
  Option('industrial', 'Industrial'),
  Option('land', 'Land'),
];
const ownershipTypes = [Option('owned', 'Owned'), Option('managedForOwner', 'Managed for owner')];

// ---- Properties -------------------------------------------------------------------------------------------------------------------

FormSpec propertyForm(WidgetRef ref, {Json? existing}) {
  final me = ref.read(meOrEmptyProvider);
  final editing = existing != null;
  return FormSpec(
    title: editing ? 'Edit ${existing['name']}' : 'New property',
    initial: editing
        ? {
            for (final k in ['legalEntityId', 'name', 'propertyType', 'ownershipType', 'ownerPartyId', 'region', 'community', 'plotNumber', 'yearBuilt', 'totalUnits', 'indicativeValue', 'managementFeePercent', 'notes'])
              k: existing[k],
            '__label_ownerPartyId': existing['ownerName'],
          }
        : const {},
    fields: [
      Field.select('legalEntityId', 'Legal entity', options: entityOptions(me), required: true, initial: me.defaultEntity?.id),
      Field.text('name', 'Name', required: true, maxLength: 200),
      Field.select('propertyType', 'Type', options: propertyTypes, required: true, initial: 'residential'),
      Field.select('ownershipType', 'Ownership', options: ownershipTypes, required: true, initial: 'owned'),
      Field.lookup('ownerPartyId', 'Owner (for managed properties)', path: '/api/v1/parties', subtitleKey: 'code', visibleWhen: (v) => v['ownershipType'] == 'managedForOwner'),
      Field.decimal('managementFeePercent', 'Management fee %', min: 0, max: 100, decimals: 2, visibleWhen: (v) => v['ownershipType'] == 'managedForOwner'),
      Field.text('region', 'Region', maxLength: 100),
      Field.text('community', 'Community', maxLength: 100),
      Field.text('plotNumber', 'Plot number', maxLength: 50),
      Field.integer('yearBuilt', 'Year built', min: 1800, max: 2200),
      Field.integer('totalUnits', 'Total units', required: true, min: 0, initial: '0'),
      Field.money('indicativeValue', 'Indicative value', required: true, initial: '0'),
      Field.multiline('notes', 'Notes', maxLength: 1000),
    ],
    submit: (ref, body, raw) async {
      final api = ref.read(apiProvider);
      return editing ? api.put('$_propertiesPath/${existing['id']}', body: body) : api.post(_propertiesPath, body: body);
    },
  );
}

Widget propertyTile(BuildContext context, Json p) => reTile(
      context,
      title: '${p['name']}',
      subtitle: '${p['code']} · ${humanize(p['propertyType']?.toString())}${(p['region'] ?? '').toString().isEmpty ? '' : ' · ${p['region']}'}',
      trailing: '${p['totalUnits'] ?? 0} units',
      status: p['isActive'] == false ? 'inactive' : null,
      onTap: () => reGo(context, PropertyDetailScreen(id: p['id'].toString())),
    );

class PropertiesScreen extends StatelessWidget {
  const PropertiesScreen({super.key});

  @override
  Widget build(BuildContext context) => Consumer(
        builder: (context, ref, _) => Scaffold(
          appBar: AppBar(title: const Text('Properties'), actions: [
            if (reCan(ref, 'finance.reports.read')) IconButton(tooltip: 'Reports', icon: const Icon(Icons.assessment_outlined), onPressed: () => reGo(context, const ReportsScreen())),
          ]),
          body: ListScreen(
            title: 'Properties',
            showAppBar: false,
            path: _propertiesPath,
            searchHint: 'Search properties',
            emptyText: 'No properties found.',
            itemBuilder: propertyTile,
            filters: propertyTypes,
            filterKey: 'propertyType',
            create: (ref) => propertyForm(ref),
            createPermission: 'realestate.properties.manage',
          ),
        ),
      );
}

class PropertyDetailScreen extends StatelessWidget {
  const PropertyDetailScreen({super.key, required this.id});

  final String id;

  @override
  Widget build(BuildContext context) {
    return Consumer(builder: (context, ref, _) {
      return DetailPage(
        path: '$_propertiesPath/$id',
        titleOf: (d) => '${(d['property'] as Json)['name']}',
        canEdit: (d) => reCan(ref, 'realestate.properties.manage'),
        edit: (d) => propertyForm(ref, existing: d['property'] as Json),
        body: (context, d) => propertyBody(context, d),
        actions: [
          ActionSpec(
            label: 'Add unit',
            icon: Icons.add_home_outlined,
            permission: 'realestate.units.manage',
            form: (d) => unitForm(ref, propertyId: id, propertyName: '${(d['property'] as Json)['name']}'),
            run: (ref, d, _) async => null,
          ),
          ActionSpec(
            label: 'New work order',
            icon: Icons.build_outlined,
            permission: 'realestate.workorders.create',
            form: (d) => workOrderForm(ref, propertyId: id, propertyName: '${(d['property'] as Json)['name']}'),
            run: (ref, d, _) async => null,
          ),
        ],
      );
    });
  }
}

Widget propertyBody(BuildContext context, Json d) {
  final p = d['property'] as Json;
  return DetailBody(children: [
    DetailHeader(title: '${p['name']}', subtitle: '${p['code']} · ${humanize(p['propertyType']?.toString())}', status: p['isActive'] == false ? 'inactive' : 'active'),
    InfoCard(title: 'Occupancy', rows: [
      InfoRow('Registered units', '${d['registeredUnits'] ?? 0} of ${p['totalUnits'] ?? 0}'),
      InfoRow('Occupied', '${d['occupiedUnits'] ?? 0}'),
      InfoRow('Occupancy', '${formatMoney((d['occupancyPercent'] as num?) ?? 0)}%', bold: true),
      InfoRow('Contracted annual rent', formatMoney((d['contractedAnnualRent'] as num?) ?? 0)),
      InfoRow('Gross yield', '${formatMoney((d['grossYieldPercent'] as num?) ?? 0)}%'),
    ]),
    if (d['unitsNotFullyRegistered'] == true)
      const Padding(padding: EdgeInsets.only(bottom: 12), child: Text('Not all units of this property are registered yet.', style: TextStyle(fontStyle: FontStyle.italic))),
    InfoCard(title: 'Details', rows: [
      InfoRow('Ownership', humanize(p['ownershipType']?.toString())),
      InfoRow('Management fee', (p['managementFeePercent'] as num?) == null || p['managementFeePercent'] == 0 ? null : '${p['managementFeePercent']}%'),
      InfoRow('Region', text(p['region'])),
      InfoRow('Community', text(p['community'])),
      InfoRow('Plot number', text(p['plotNumber'])),
      InfoRow('Year built', text(p['yearBuilt'])),
      InfoRow('Indicative value', formatMoney((p['indicativeValue'] as num?) ?? 0)),
    ]),
    if ((p['notes'] ?? '').toString().isNotEmpty) Text(p['notes'].toString()),
    const SizedBox(height: 8),
    Text('Units', style: Theme.of(context).textTheme.titleMedium),
    RelatedList(path: _unitsPath, query: {'filter': 'propertyId:eq:${p['id']}', 'pageSize': 50}, tile: unitTile, empty: 'No units registered.'),
  ]);
}

// ---- Units ------------------------------------------------------------------------------------------------------------------------

FormSpec unitForm(WidgetRef ref, {Json? existing, String? propertyId, String? propertyName}) {
  final editing = existing != null;
  return FormSpec(
    title: editing ? 'Edit unit ${existing['unitNumber']}' : 'New unit',
    initial: {
      'propertyId': ?propertyId,
      '__label_propertyId': ?propertyName,
      if (editing) ...{
        for (final k in ['propertyId', 'unitNumber', 'unitTypeId', 'usage', 'floor', 'bedrooms', 'areaSqft', 'askingRent', 'notes']) k: existing[k],
        '__label_propertyId': existing['propertyName'],
        '__label_unitTypeId': existing['unitTypeName'],
      },
    },
    fields: [
      // A unit cannot move property, so the property is fixed on edit.
      if (!editing && propertyId == null) propertyLookup(),
      Field.text('unitNumber', 'Unit number', required: true, maxLength: 50),
      Field.lookup('unitTypeId', 'Unit type', path: _unitTypesPath, subtitleKey: 'code', required: true),
      Field.select('usage', 'Usage (defaults to the unit type)', options: reUsages),
      Field.text('floor', 'Floor', maxLength: 20),
      Field.integer('bedrooms', 'Bedrooms', min: 0),
      Field.decimal('areaSqft', 'Area (sqft)', required: true, min: 0, decimals: 2, initial: '0'),
      Field.money('askingRent', 'Asking rent (per year)', required: true, initial: '0'),
      Field.multiline('notes', 'Notes', maxLength: 1000),
    ],
    submit: (ref, body, raw) async {
      final api = ref.read(apiProvider);
      if (propertyId != null || editing) body['propertyId'] = propertyId ?? existing!['propertyId'];
      return editing ? api.put('$_unitsPath/${existing['id']}', body: body) : api.post(_unitsPath, body: body);
    },
  );
}

Widget unitTile(BuildContext context, Json u) {
  final status = (u['status'] ?? '').toString();
  final tenant = (u['tenantName'] ?? '').toString();
  final extra = status == 'occupied' && tenant.isNotEmpty ? '$tenant · ends ${formatDate(u['leaseEnds'])}' : (u['daysVacant'] != null ? 'Vacant ${u['daysVacant']} days' : '${u['unitTypeName'] ?? ''}');
  return reTile(
    context,
    title: 'Unit ${u['unitNumber']} · ${u['propertyName'] ?? ''}',
    subtitle: extra,
    trailing: formatMoney((u['currentRent'] ?? u['askingRent'] ?? 0) as num),
    status: status,
    onTap: () => reGo(context, UnitDetailScreen(id: u['id'].toString())),
  );
}

class UnitsScreen extends StatelessWidget {
  const UnitsScreen({super.key});

  @override
  Widget build(BuildContext context) => ListScreen(
        title: 'Units',
        path: _unitsPath,
        searchHint: 'Search units',
        emptyText: 'No units found.',
        itemBuilder: unitTile,
        filters: reUnitStatuses,
        create: (ref) => unitForm(ref),
        createPermission: 'realestate.units.manage',
      );
}

FormSpec _unitStatusForm(String id, String title, String action, List<Field> fields, {String submit = 'Save'}) => FormSpec(
      title: title,
      submitLabel: submit,
      fields: fields,
      submit: (ref, body, raw) => ref.read(apiProvider).post('$_unitsPath/$id/status', body: {'action': action, ...body}),
    );

class UnitDetailScreen extends StatelessWidget {
  const UnitDetailScreen({super.key, required this.id});

  final String id;

  @override
  Widget build(BuildContext context) {
    return Consumer(builder: (context, ref, _) {
      return DetailPage(
        path: '$_unitsPath/$id',
        titleOf: (d) => 'Unit ${d['unitNumber']}',
        canEdit: (d) => reCan(ref, 'realestate.units.manage'),
        edit: (d) => unitForm(ref, existing: {...d, 'propertyName': null, 'unitTypeName': null}),
        body: (context, d) => _body(context, d),
        actions: [
          ActionSpec(
            label: 'Reserve',
            icon: Icons.bookmark_add_outlined,
            permission: 'realestate.units.reserve',
            visible: (d) => d['status'] == 'vacant',
            form: (d) => _unitStatusForm(id, 'Reserve unit', 'reserve', [Field.date('until', 'Reserved until', required: true)], submit: 'Reserve'),
            run: (ref, d, _) async => null,
          ),
          ActionSpec(
            label: 'Release',
            icon: Icons.bookmark_remove_outlined,
            permission: 'realestate.units.reserve',
            visible: (d) => d['status'] == 'reserved',
            confirm: 'Release this reservation?',
            run: (ref, d, _) => ref.read(apiProvider).post('$_unitsPath/$id/status', body: {'action': 'releaseReservation'}),
          ),
          ActionSpec(
            label: 'Make unavailable',
            icon: Icons.block,
            permission: 'realestate.units.manage',
            destructive: true,
            visible: (d) => d['status'] == 'vacant',
            form: (d) => _unitStatusForm(id, 'Make unavailable', 'makeUnavailable', [Field.text('reason', 'Reason', required: true, maxLength: 200)]),
            run: (ref, d, _) async => null,
          ),
          ActionSpec(
            label: 'Make available',
            icon: Icons.check_circle_outline,
            permission: 'realestate.units.manage',
            visible: (d) => d['status'] == 'unavailable',
            confirm: 'Put this unit back on the market?',
            run: (ref, d, _) => ref.read(apiProvider).post('$_unitsPath/$id/status', body: {'action': 'makeAvailable'}),
          ),
        ],
      );
    });
  }

  Widget _body(BuildContext context, Json d) => DetailBody(children: [
        DetailHeader(title: 'Unit ${d['unitNumber']}', subtitle: '${humanize(d['usage']?.toString())}${(d['floor'] ?? '').toString().isEmpty ? '' : ' · floor ${d['floor']}'}', status: d['status']?.toString()),
        InfoCard(rows: [
          InfoRow('Asking rent / year', formatMoney((d['askingRent'] as num?) ?? 0), bold: true),
          InfoRow('Area', '${formatMoney((d['areaSqft'] as num?) ?? 0)} sqft'),
          InfoRow('Bedrooms', text(d['bedrooms'])),
          InfoRow('Vacant since', d['vacantSince'] == null ? null : '${formatDate(d['vacantSince'])} (${d['daysVacant'] ?? 0} days)'),
          InfoRow('Reserved until', d['reservedUntil'] == null ? null : formatDate(d['reservedUntil'])),
          InfoRow('Unavailable because', text(d['unavailableReason'])),
        ]),
        if ((d['notes'] ?? '').toString().isNotEmpty) Text(d['notes'].toString()),
        const SizedBox(height: 8),
        Text('Leases', style: Theme.of(context).textTheme.titleMedium),
        RelatedList(path: '$rePath/leases', query: {'filter': 'unitId:eq:${d['id']}', 'pageSize': 20}, tile: leaseTile, empty: 'No leases for this unit.'),
        const SizedBox(height: 8),
        Text('Rent history', style: Theme.of(context).textTheme.titleMedium),
        RelatedList(
          path: '$_unitsPath/${d['id']}/rent-history',
          tile: (c, r) => ListTile(dense: true, contentPadding: EdgeInsets.zero, title: Text('From ${formatDate(r['effectiveFrom'])}'), subtitle: r['reason'] == null ? null : Text('${r['reason']}'), trailing: Text(formatMoney((r['annualRent'] as num?) ?? 0))),
          empty: 'No rent changes recorded.',
        ),
      ]);
}

// ---- Unit types -------------------------------------------------------------------------------------------------------------------

FormSpec unitTypeForm(WidgetRef ref, {Json? existing}) {
  final editing = existing != null;
  return FormSpec(
    title: editing ? 'Edit ${existing['name']}' : 'New unit type',
    initial: editing ? {for (final k in ['code', 'name', 'defaultUsage', 'defaultBedrooms', 'isActive']) k: existing[k]} : const {},
    fields: [
      Field.text('code', 'Code', required: true, maxLength: 30),
      Field.text('name', 'Name', required: true, maxLength: 100),
      Field.select('defaultUsage', 'Default usage', options: reUsages, required: true, initial: 'residential'),
      Field.integer('defaultBedrooms', 'Default bedrooms', min: 0),
      Field.toggle('isActive', 'Active', initial: true),
    ],
    submit: (ref, body, raw) async {
      final api = ref.read(apiProvider);
      return editing ? api.put('$_unitTypesPath/${existing['id']}', body: body) : api.post(_unitTypesPath, body: body);
    },
  );
}

class UnitTypesScreen extends StatelessWidget {
  const UnitTypesScreen({super.key});

  @override
  Widget build(BuildContext context) => Consumer(
        builder: (context, ref, _) => ListScreen(
          title: 'Unit types',
          path: _unitTypesPath,
          searchHint: 'Search unit types',
          emptyText: 'No unit types found.',
          itemBuilder: (context, t) => ListTile(
            title: Text('${t['name']}'),
            subtitle: Text('${t['code']} · ${humanize(t['defaultUsage']?.toString())}${t['defaultBedrooms'] == null ? '' : ' · ${t['defaultBedrooms']} bed'}'),
            trailing: t['isActive'] == false ? reChip('inactive') : null,
            onTap: reCan(ref, 'realestate.unittypes.manage') ? () => openForm<Object?>(context, unitTypeForm(ref, existing: t)) : null,
          ),
          create: (ref) => unitTypeForm(ref),
          createPermission: 'realestate.unittypes.manage',
        ),
      );
}
