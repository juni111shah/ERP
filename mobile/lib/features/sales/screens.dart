import 'package:flutter/material.dart';

import '../../kit/registry.dart';
import 'distribution_screens.dart';
import 'order_screens.dart';
import 'quote_screens.dart';
import 'retail_screens.dart';

/// Screens of this area for the More menu (Sales, Retail, Distribution).
final List<MobileScreen> salesScreens = <MobileScreen>[
  MobileScreen(id: 'sales-quotes', title: 'Quotes', menuIds: const ['sales.quotes'], icon: Icons.description_outlined, builder: (_) => const QuotesScreen()),
  MobileScreen(id: 'sales-orders', title: 'Sales orders', menuIds: const ['sales.orders'], icon: Icons.shopping_cart_outlined, builder: (_) => const OrdersScreen()),
  MobileScreen(id: 'retail-stores', title: 'Stores', menuIds: const ['retail.stores'], icon: Icons.storefront_outlined, builder: (_) => const StoresScreen()),
  MobileScreen(id: 'retail-pricelists', title: 'Price lists', menuIds: const ['retail.pricelists'], icon: Icons.sell_outlined, builder: (_) => const PriceListsScreen()),
  MobileScreen(id: 'retail-promotions', title: 'Promotions', menuIds: const ['retail.promotions'], icon: Icons.card_giftcard_outlined, builder: (_) => const PromotionsScreen()),
  MobileScreen(id: 'distribution-routes', title: 'Routes', menuIds: const ['distribution.routes'], icon: Icons.alt_route, builder: (_) => const RoutesScreen()),
  MobileScreen(id: 'distribution-trips', title: 'Van sales', menuIds: const ['distribution.trips'], icon: Icons.local_shipping_outlined, builder: (_) => const TripsScreen()),
  MobileScreen(id: 'distribution-wholesale', title: 'Wholesale pricing', menuIds: const ['distribution.wholesale'], icon: Icons.percent, builder: (_) => const WholesaleScreen()),
];
