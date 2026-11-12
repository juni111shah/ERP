import { Injectable, inject, signal } from '@angular/core';
import { AssetCategoriesApi, AssetCategoryDto } from '@core/api/assets.api';
import { loadInto } from '@shared/utils/load-into';

/** The asset categories (first 200 by code) the asset pages name and pick from. Provided per page (`providers: [AssetLookups]`). */
@Injectable()
export class AssetLookups {
  private readonly categoriesApi = inject(AssetCategoriesApi);

  readonly categories = signal<AssetCategoryDto[]>([]);

  /** Loads the categories; a failure leaves the list empty. */
  loadCategories(): void {
    loadInto(this.categories, this.categoriesApi.list({ page: 1, pageSize: 200, sort: 'code' }), (r) => r.items);
  }

  category(id: string): AssetCategoryDto | undefined {
    return this.categories().find((c) => c.id === id);
  }
}
