import { ASSET_STATUSES, AssetCategoryDto, AssetDto, DEPRECIATION_METHODS } from '@core/api/assets.api';
import { DescriptionItem } from '@shared/components/detail-page/detail-page.component';

/** How asset values are named and shown on the asset pages. */

/** The label of a value from a list of options, or the value itself when it is not one of them. */
function labelOf(options: { value: string; label: string }[], value: string): string {
  return options.find((o) => o.value === value)?.label ?? value;
}

/** "In service", "Disposed"... */
export function assetStatusLabel(status: string): string {
  return labelOf(ASSET_STATUSES, status);
}

/** "Straight line" or "Declining balance". */
export function depreciationMethodLabel(method: string): string {
  return labelOf(DEPRECIATION_METHODS, method);
}

/** Money with two decimals, as the KPI cards show it. */
export function assetMoney(value: number): string {
  return new Intl.NumberFormat('en-US', { minimumFractionDigits: 2, maximumFractionDigits: 2 }).format(value);
}

/** The facts of an asset on its detail page; the disposal lines appear once it is disposed of. */
export function assetDescriptions(a: AssetDto, category: AssetCategoryDto | undefined): DescriptionItem[] {
  const items: DescriptionItem[] = [
    { label: 'Category', value: category?.name ?? '—' },
    { label: 'Status', value: assetStatusLabel(a.status) },
    { label: 'Acquired', value: a.acquisitionDate },
    { label: 'Method', value: depreciationMethodLabel(a.method) },
    { label: 'Useful life (months)', value: a.lifeMonths },
    { label: 'Custodian', value: a.custodian },
  ];
  if (a.capitalisedOn) items.push({ label: 'In service since', value: a.capitalisedOn });
  if (a.description) items.push({ label: 'Description', value: a.description, span: 2 });
  if (a.disposedOn) {
    items.push({ label: 'Disposed on', value: a.disposedOn });
    items.push({ label: 'Proceeds', value: a.disposalProceeds ?? 0 });
    items.push({ label: (a.disposalGainLoss ?? 0) >= 0 ? 'Gain on disposal' : 'Loss on disposal', value: Math.abs(a.disposalGainLoss ?? 0) });
  }
  return items;
}
