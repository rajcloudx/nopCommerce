﻿using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Nop.Core;
using Nop.Core.Domain.Catalog;
using Nop.Core.Domain.Common;
using Nop.Core.Domain.Customers;
using Nop.Core.Domain.Discounts;
using Nop.Core.Domain.Orders;
using Nop.Core.Domain.Shipping;
using Nop.Core.Domain.Tax;
using Nop.Services.Catalog;
using Nop.Services.Common;
using Nop.Services.Customers;
using Nop.Services.Discounts;
using Nop.Services.Payments;
using Nop.Services.Shipping;
using Nop.Services.Tax;

namespace Nop.Services.Orders
{
    /// <summary>
    /// Order service
    /// </summary>
    public partial class OrderTotalCalculationService : IOrderTotalCalculationService
    {
        #region Fields

        protected CatalogSettings CatalogSettings { get; }
        protected IAddressService AddressService { get; }
        protected ICheckoutAttributeParser CheckoutAttributeParser { get; }
        protected ICustomerService CustomerService { get; }
        protected IDiscountService DiscountService { get; }
        protected IGenericAttributeService GenericAttributeService { get; }
        protected IGiftCardService GiftCardService { get; }
        protected IOrderService OrderService { get; }
        protected IPaymentService PaymentService { get; }
        protected IPriceCalculationService PriceCalculationService { get; }
        protected IProductService ProductService { get; }
        protected IRewardPointService RewardPointService { get; }
        protected IShippingPluginManager ShippingPluginManager { get; }
        protected IShippingService ShippingService { get; }
        protected IShoppingCartService ShoppingCartService { get; }
        protected IStoreContext StoreContext { get; }
        protected ITaxService TaxService { get; }
        protected IWorkContext WorkContext { get; }
        protected RewardPointsSettings RewardPointsSettings { get; }
        protected ShippingSettings ShippingSettings { get; }
        protected ShoppingCartSettings ShoppingCartSettings { get; }
        protected TaxSettings TaxSettings { get; }

        #endregion

        #region Ctor

        public OrderTotalCalculationService(CatalogSettings catalogSettings,
            IAddressService addressService,
            ICheckoutAttributeParser checkoutAttributeParser,
            ICustomerService customerService,
            IDiscountService discountService,
            IGenericAttributeService genericAttributeService,
            IGiftCardService giftCardService,
            IOrderService orderService,
            IPaymentService paymentService,
            IPriceCalculationService priceCalculationService,
            IProductService productService,
            IRewardPointService rewardPointService,
            IShippingPluginManager shippingPluginManager,
            IShippingService shippingService,
            IShoppingCartService shoppingCartService,
            IStoreContext storeContext,
            ITaxService taxService,
            IWorkContext workContext,
            RewardPointsSettings rewardPointsSettings,
            ShippingSettings shippingSettings,
            ShoppingCartSettings shoppingCartSettings,
            TaxSettings taxSettings)
        {
            CatalogSettings = catalogSettings;
            AddressService = addressService;
            CheckoutAttributeParser = checkoutAttributeParser;
            CustomerService = customerService;
            DiscountService = discountService;
            GenericAttributeService = genericAttributeService;
            GiftCardService = giftCardService;
            OrderService = orderService;
            PaymentService = paymentService;
            PriceCalculationService = priceCalculationService;
            ProductService = productService;
            RewardPointService = rewardPointService;
            ShippingPluginManager = shippingPluginManager;
            ShippingService = shippingService;
            ShoppingCartService = shoppingCartService;
            StoreContext = storeContext;
            TaxService = taxService;
            this.WorkContext = workContext;
            RewardPointsSettings = rewardPointsSettings;
            ShippingSettings = shippingSettings;
            ShoppingCartSettings = shoppingCartSettings;
            TaxSettings = taxSettings;
        }

        #endregion

        #region Utilities

        /// <summary>
        /// Gets an order discount (applied to order subtotal)
        /// </summary>
        /// <param name="customer">Customer</param>
        /// <param name="orderSubTotal">Order subtotal</param>
        /// <returns>
        /// A task that represents the asynchronous operation
        /// The task result contains the order discount, Applied discounts
        /// </returns>
        protected virtual async Task<(decimal orderDiscount, List<Discount> appliedDiscounts)> GetOrderSubtotalDiscountAsync(Customer customer,
            decimal orderSubTotal)
        {
            var appliedDiscounts = new List<Discount>();
            var discountAmount = decimal.Zero;
            if (CatalogSettings.IgnoreDiscounts)
                return (discountAmount, appliedDiscounts);

            var allDiscounts = await DiscountService.GetAllDiscountsAsync(DiscountType.AssignedToOrderSubTotal);
            var allowedDiscounts = new List<Discount>();
            if (allDiscounts != null)
            {
                foreach (var discount in allDiscounts)
                    if (!DiscountService.ContainsDiscount(allowedDiscounts, discount) &&
                        (await DiscountService.ValidateDiscountAsync(discount, customer)).IsValid)
                    {
                        allowedDiscounts.Add(discount);
                    }
            }

            appliedDiscounts = DiscountService.GetPreferredDiscount(allowedDiscounts, orderSubTotal, out discountAmount);

            if (discountAmount < decimal.Zero)
                discountAmount = decimal.Zero;

            return (discountAmount, appliedDiscounts);
        }

        /// <summary>
        /// Gets a shipping discount
        /// </summary>
        /// <param name="customer">Customer</param>
        /// <param name="shippingTotal">Shipping total</param>
        /// <returns>
        /// A task that represents the asynchronous operation
        /// The task result contains the shipping discount. Applied discounts
        /// </returns>
        protected virtual async Task<(decimal shippingDiscount, List<Discount> appliedDiscounts)> GetShippingDiscountAsync(Customer customer, decimal shippingTotal)
        {
            var appliedDiscounts = new List<Discount>();
            var shippingDiscountAmount = decimal.Zero;
            if (CatalogSettings.IgnoreDiscounts)
                return (shippingDiscountAmount, appliedDiscounts);

            var allDiscounts = await DiscountService.GetAllDiscountsAsync(DiscountType.AssignedToShipping);
            var allowedDiscounts = new List<Discount>();
            if (allDiscounts != null)
                foreach (var discount in allDiscounts)
                    if (!DiscountService.ContainsDiscount(allowedDiscounts, discount) &&
                        (await DiscountService.ValidateDiscountAsync(discount, customer)).IsValid)
                    {
                        allowedDiscounts.Add(discount);
                    }

            appliedDiscounts = DiscountService.GetPreferredDiscount(allowedDiscounts, shippingTotal, out shippingDiscountAmount);

            if (shippingDiscountAmount < decimal.Zero)
                shippingDiscountAmount = decimal.Zero;

            if (ShoppingCartSettings.RoundPricesDuringCalculation)
                shippingDiscountAmount = await PriceCalculationService.RoundPriceAsync(shippingDiscountAmount);

            return (shippingDiscountAmount, appliedDiscounts);
        }

        /// <summary>
        /// Gets an order discount (applied to order total)
        /// </summary>
        /// <param name="customer">Customer</param>
        /// <param name="orderTotal">Order total</param>
        /// <returns>
        /// A task that represents the asynchronous operation
        /// The task result contains the order discount. Applied discounts
        /// </returns>
        protected virtual async Task<(decimal orderDiscount, List<Discount> appliedDiscounts)> GetOrderTotalDiscountAsync(Customer customer, decimal orderTotal)
        {
            var appliedDiscounts = new List<Discount>();
            var discountAmount = decimal.Zero;
            if (CatalogSettings.IgnoreDiscounts)
                return (discountAmount, appliedDiscounts);

            var allDiscounts = await DiscountService.GetAllDiscountsAsync(DiscountType.AssignedToOrderTotal);
            var allowedDiscounts = new List<Discount>();
            if (allDiscounts != null)
                foreach (var discount in allDiscounts)
                    if (!DiscountService.ContainsDiscount(allowedDiscounts, discount) &&
                        (await DiscountService.ValidateDiscountAsync(discount, customer)).IsValid)
                    {
                        allowedDiscounts.Add(discount);
                    }

            appliedDiscounts = DiscountService.GetPreferredDiscount(allowedDiscounts, orderTotal, out discountAmount);

            if (discountAmount < decimal.Zero)
                discountAmount = decimal.Zero;

            if (ShoppingCartSettings.RoundPricesDuringCalculation)
                discountAmount = await PriceCalculationService.RoundPriceAsync(discountAmount);

            return (discountAmount, appliedDiscounts);
        }

        /// <summary>
        /// Update order total
        /// </summary>
        /// <param name="updateOrderParameters">UpdateOrderParameters</param>
        /// <param name="subTotalExclTax">Subtotal (excl tax)</param>
        /// <param name="discountAmountExclTax">Discount amount (excl tax)</param>
        /// <param name="shippingTotalExclTax">Shipping (excl tax)</param>
        /// <param name="taxTotal">Tax</param>
        /// <returns>A task that represents the asynchronous operation</returns>
        protected virtual async Task UpdateTotalAsync(UpdateOrderParameters updateOrderParameters, decimal subTotalExclTax,
            decimal discountAmountExclTax, decimal shippingTotalExclTax, decimal taxTotal)
        {
            var updatedOrder = updateOrderParameters.UpdatedOrder;
            var customer = await CustomerService.GetCustomerByIdAsync(updatedOrder.CustomerId);

            var total = subTotalExclTax - discountAmountExclTax + shippingTotalExclTax + updatedOrder.PaymentMethodAdditionalFeeExclTax + taxTotal;

            //get discounts for the order total
            var (discountAmountTotal, orderAppliedDiscounts) = await GetOrderTotalDiscountAsync(customer, total);
            if (total < discountAmountTotal)
                discountAmountTotal = total;
            total -= discountAmountTotal;

            //applied giftcards
            foreach (var giftCard in await GiftCardService.GetAllGiftCardsAsync(usedWithOrderId: updatedOrder.Id))
            {
                if (total <= decimal.Zero)
                    continue;

                var remainingAmount = (await GiftCardService.GetGiftCardUsageHistoryAsync(giftCard))
                    .Where(history => history.UsedWithOrderId == updatedOrder.Id).Sum(history => history.UsedValue);
                var amountCanBeUsed = total > remainingAmount ? remainingAmount : total;
                total -= amountCanBeUsed;
            }

            //reward points
            var rewardPointsOfOrder = await RewardPointService.GetRewardPointsHistoryEntryByIdAsync(updatedOrder.RedeemedRewardPointsEntryId ?? 0);
            if (rewardPointsOfOrder != null)
            {
                var rewardPoints = -rewardPointsOfOrder.Points;
                var rewardPointsAmount = await ConvertRewardPointsToAmountAsync(rewardPoints);
                if (total < rewardPointsAmount)
                {
                    rewardPoints = ConvertAmountToRewardPoints(total);
                    rewardPointsAmount = total;
                }

                if (total > decimal.Zero)
                    total -= rewardPointsAmount;

                //uncomment here for the return unused reward points if new order total less redeemed reward points amount
                //if (rewardPoints < -rewardPointsOfOrder.Points)
                //    _rewardPointService.AddRewardPointsHistoryEntry(customer, -rewardPointsOfOrder.Points - rewardPoints, store.Id, "Return unused reward points");

                if (rewardPointsAmount != rewardPointsOfOrder.UsedAmount)
                {
                    rewardPointsOfOrder.UsedAmount = rewardPointsAmount;
                    rewardPointsOfOrder.Points = -rewardPoints;
                    await RewardPointService.UpdateRewardPointsHistoryEntryAsync(rewardPointsOfOrder);
                }
            }

            //rounding
            if (total < decimal.Zero)
                total = decimal.Zero;
            if (ShoppingCartSettings.RoundPricesDuringCalculation)
                total = await PriceCalculationService.RoundPriceAsync(total);

            updatedOrder.OrderDiscount = discountAmountTotal;
            updatedOrder.OrderTotal = total;

            foreach (var discount in orderAppliedDiscounts)
                if (!DiscountService.ContainsDiscount(updateOrderParameters.AppliedDiscounts, discount))
                    updateOrderParameters.AppliedDiscounts.Add(discount);
        }

        /// <summary>
        /// Update tax rates
        /// </summary>
        /// <param name="subTotalTaxRates">Subtotal tax rates</param>
        /// <param name="shippingTotalInclTax">Shipping (incl tax)</param>
        /// <param name="shippingTotalExclTax">Shipping (excl tax)</param>
        /// <param name="shippingTaxRate">Shipping tax rates</param>
        /// <param name="updatedOrder">Order</param>
        /// <returns>
        /// A task that represents the asynchronous operation
        /// The task result contains the ax total
        /// </returns>
        protected virtual async Task<decimal> UpdateTaxRatesAsync(SortedDictionary<decimal, decimal> subTotalTaxRates, decimal shippingTotalInclTax,
            decimal shippingTotalExclTax, decimal shippingTaxRate, Order updatedOrder)
        {
            var taxRates = new SortedDictionary<decimal, decimal>();

            //order subtotal taxes
            var subTotalTax = decimal.Zero;
            foreach (var kvp in subTotalTaxRates)
            {
                subTotalTax += kvp.Value;
                if (kvp.Key <= decimal.Zero || kvp.Value <= decimal.Zero)
                    continue;

                if (!taxRates.ContainsKey(kvp.Key))
                    taxRates.Add(kvp.Key, kvp.Value);
                else
                    taxRates[kvp.Key] = taxRates[kvp.Key] + kvp.Value;
            }

            //shipping taxes
            var shippingTax = decimal.Zero;
            if (TaxSettings.ShippingIsTaxable)
            {
                shippingTax = shippingTotalInclTax - shippingTotalExclTax;
                if (shippingTax < decimal.Zero)
                    shippingTax = decimal.Zero;

                if (shippingTaxRate > decimal.Zero && shippingTax > decimal.Zero)
                {
                    if (!taxRates.ContainsKey(shippingTaxRate))
                        taxRates.Add(shippingTaxRate, shippingTax);
                    else
                        taxRates[shippingTaxRate] = taxRates[shippingTaxRate] + shippingTax;
                }
            }

            //payment method additional fee tax
            var paymentMethodAdditionalFeeTax = decimal.Zero;
            if (TaxSettings.PaymentMethodAdditionalFeeIsTaxable)
            {
                paymentMethodAdditionalFeeTax = updatedOrder.PaymentMethodAdditionalFeeInclTax - updatedOrder.PaymentMethodAdditionalFeeExclTax;
                if (paymentMethodAdditionalFeeTax < decimal.Zero)
                    paymentMethodAdditionalFeeTax = decimal.Zero;

                if (updatedOrder.PaymentMethodAdditionalFeeExclTax > decimal.Zero)
                {
                    var paymentTaxRate = Math.Round(100 * paymentMethodAdditionalFeeTax / updatedOrder.PaymentMethodAdditionalFeeExclTax, 3);
                    if (paymentTaxRate > decimal.Zero && paymentMethodAdditionalFeeTax > decimal.Zero)
                    {
                        if (!taxRates.ContainsKey(paymentTaxRate))
                            taxRates.Add(paymentTaxRate, paymentMethodAdditionalFeeTax);
                        else
                            taxRates[paymentTaxRate] = taxRates[paymentTaxRate] + paymentMethodAdditionalFeeTax;
                    }
                }
            }

            //add at least one tax rate (0%)
            if (!taxRates.Any())
                taxRates.Add(decimal.Zero, decimal.Zero);

            //summarize taxes
            var taxTotal = subTotalTax + shippingTax + paymentMethodAdditionalFeeTax;
            if (taxTotal < decimal.Zero)
                taxTotal = decimal.Zero;

            //round tax
            if (ShoppingCartSettings.RoundPricesDuringCalculation)
                taxTotal = await PriceCalculationService.RoundPriceAsync(taxTotal);

            updatedOrder.OrderTax = taxTotal;
            updatedOrder.TaxRates = taxRates.Aggregate(string.Empty, (current, next) =>
                $"{current}{next.Key.ToString(CultureInfo.InvariantCulture)}:{next.Value.ToString(CultureInfo.InvariantCulture)};   ");
            
            return taxTotal;
        }

        /// <summary>
        /// Update shipping
        /// </summary>
        /// <param name="updateOrderParameters">UpdateOrderParameters</param>
        /// <param name="restoredCart">Cart</param>
        /// <param name="subTotalInclTax">Subtotal (incl tax)</param>
        /// <param name="subTotalExclTax">Subtotal (excl tax)</param>
        /// <returns>
        /// A task that represents the asynchronous operation
        /// The task result contains the shipping total. Shipping (incl tax). Shipping tax rate
        /// </returns>
        protected virtual async Task<(decimal shippingTotal, decimal shippingTotalInclTax, decimal shippingTaxRate)> UpdateShippingAsync(UpdateOrderParameters updateOrderParameters, IList<ShoppingCartItem> restoredCart,
            decimal subTotalInclTax, decimal subTotalExclTax)
        {
            var shippingTotalExclTax = decimal.Zero;
            var shippingTotalInclTax = decimal.Zero;
            var shippingTaxRate = decimal.Zero;

            var updatedOrder = updateOrderParameters.UpdatedOrder;
            var customer = await CustomerService.GetCustomerByIdAsync(updatedOrder.CustomerId);
            var currentCustomer = await WorkContext.GetCurrentCustomerAsync();
            var store = await StoreContext.GetCurrentStoreAsync();

            if (await ShoppingCartService.ShoppingCartRequiresShippingAsync(restoredCart))
            {
                if (!await IsFreeShippingAsync(restoredCart, ShippingSettings.FreeShippingOverXIncludingTax ? subTotalInclTax : subTotalExclTax))
                {
                    var shippingTotal = decimal.Zero;
                    if (!string.IsNullOrEmpty(updatedOrder.ShippingRateComputationMethodSystemName))
                    {
                        //in the updated order were shipping items
                        if (updatedOrder.PickupInStore)
                        {
                            //customer chose pickup in store method, try to get chosen pickup point
                            if (ShippingSettings.AllowPickupInStore)
                            {
                                var pickupPointsResponse = await ShippingService.GetPickupPointsAsync(updatedOrder.BillingAddressId, customer,
                                    updatedOrder.ShippingRateComputationMethodSystemName, store.Id);
                                if (pickupPointsResponse.Success)
                                {
                                    var selectedPickupPoint =
                                        pickupPointsResponse.PickupPoints.FirstOrDefault(point =>
                                            updatedOrder.ShippingMethod.Contains(point.Name));
                                    if (selectedPickupPoint != null)
                                        shippingTotal = selectedPickupPoint.PickupFee;
                                    else
                                        updateOrderParameters.Warnings.Add(
                                            $"Shipping method {updatedOrder.ShippingMethod} could not be loaded");
                                }
                                else
                                    updateOrderParameters.Warnings.AddRange(pickupPointsResponse.Errors);
                            }
                            else
                                updateOrderParameters.Warnings.Add("Pick up in store is not available");
                        }
                        else
                        {
                            //customer chose shipping to address, try to get chosen shipping option
                            var shippingAddress = await AddressService.GetAddressByIdAsync(updatedOrder.ShippingAddressId ?? 0);
                            var shippingOptionsResponse = await ShippingService.GetShippingOptionsAsync(restoredCart, shippingAddress, customer, updatedOrder.ShippingRateComputationMethodSystemName, store.Id);
                            if (shippingOptionsResponse.Success)
                            {
                                var shippingOption = shippingOptionsResponse.ShippingOptions.FirstOrDefault(option =>
                                    updatedOrder.ShippingMethod.Contains(option.Name));
                                if (shippingOption != null)
                                    shippingTotal = shippingOption.Rate;
                                else
                                    updateOrderParameters.Warnings.Add(
                                        $"Shipping method {updatedOrder.ShippingMethod} could not be loaded");
                            }
                            else
                                updateOrderParameters.Warnings.AddRange(shippingOptionsResponse.Errors);
                        }
                    }
                    else
                    {
                        //before updating order was without shipping
                        if (ShippingSettings.AllowPickupInStore)
                        {
                            //try to get the cheapest pickup point
                            var pickupPointsResponse = await ShippingService.GetPickupPointsAsync(updatedOrder.BillingAddressId, currentCustomer, storeId: store.Id);
                            if (pickupPointsResponse.Success)
                            {
                                updateOrderParameters.PickupPoint = pickupPointsResponse.PickupPoints
                                    .OrderBy(point => point.PickupFee).First();
                                shippingTotal = updateOrderParameters.PickupPoint.PickupFee;
                            }
                            else
                                updateOrderParameters.Warnings.AddRange(pickupPointsResponse.Errors);
                        }
                        else
                            updateOrderParameters.Warnings.Add("Pick up in store is not available");

                        if (updateOrderParameters.PickupPoint == null)
                        {
                            //or try to get the cheapest shipping option for the shipping to the customer address 
                            var shippingRateComputationMethods = await ShippingPluginManager.LoadActivePluginsAsync();
                            if (shippingRateComputationMethods.Any())
                            {
                                var customerShippingAddress = await CustomerService.GetCustomerShippingAddressAsync(customer);

                                var shippingOptionsResponse = await ShippingService.GetShippingOptionsAsync(restoredCart, customerShippingAddress, currentCustomer, storeId: store.Id);
                                if (shippingOptionsResponse.Success)
                                {
                                    var shippingOption = shippingOptionsResponse.ShippingOptions.OrderBy(option => option.Rate)
                                        .First();
                                    updatedOrder.ShippingRateComputationMethodSystemName =
                                        shippingOption.ShippingRateComputationMethodSystemName;
                                    updatedOrder.ShippingMethod = shippingOption.Name;

                                    var updatedShippingAddress = AddressService.CloneAddress(customerShippingAddress);
                                    await AddressService.InsertAddressAsync(updatedShippingAddress);
                                    updatedOrder.ShippingAddressId = updatedShippingAddress.Id;

                                    shippingTotal = shippingOption.Rate;
                                }
                                else
                                    updateOrderParameters.Warnings.AddRange(shippingOptionsResponse.Errors);
                            }
                            else
                                updateOrderParameters.Warnings.Add("Shipping rate computation method could not be loaded");
                        }
                    }

                    //additional shipping charge
                    shippingTotal += await GetShoppingCartAdditionalShippingChargeAsync(restoredCart);

                    //shipping discounts
                    var (shippingDiscount, shippingTotalDiscounts) = await GetShippingDiscountAsync(customer, shippingTotal);
                    shippingTotal -= shippingDiscount;
                    if (shippingTotal < decimal.Zero)
                        shippingTotal = decimal.Zero;

                    shippingTotalExclTax = (await TaxService.GetShippingPriceAsync(shippingTotal, false, customer)).price;
                    (shippingTotalInclTax, shippingTaxRate) = await TaxService.GetShippingPriceAsync(shippingTotal, true, customer);

                    //rounding
                    if (ShoppingCartSettings.RoundPricesDuringCalculation)
                    {
                        shippingTotalExclTax = await PriceCalculationService.RoundPriceAsync(shippingTotalExclTax);
                        shippingTotalInclTax = await PriceCalculationService.RoundPriceAsync(shippingTotalInclTax);
                    }

                    //change shipping status
                    if (updatedOrder.ShippingStatus == ShippingStatus.ShippingNotRequired ||
                        updatedOrder.ShippingStatus == ShippingStatus.NotYetShipped)
                        updatedOrder.ShippingStatus = ShippingStatus.NotYetShipped;
                    else
                        updatedOrder.ShippingStatus = ShippingStatus.PartiallyShipped;

                    foreach (var discount in shippingTotalDiscounts)
                        if (!DiscountService.ContainsDiscount(updateOrderParameters.AppliedDiscounts, discount))
                            updateOrderParameters.AppliedDiscounts.Add(discount);
                }
            }
            else
                updatedOrder.ShippingStatus = ShippingStatus.ShippingNotRequired;

            updatedOrder.OrderShippingExclTax = shippingTotalExclTax;
            updatedOrder.OrderShippingInclTax = shippingTotalInclTax;

            return (shippingTotalExclTax, shippingTotalInclTax, shippingTaxRate);
        }

        /// <summary>
        /// Update order parameters
        /// </summary>
        /// <param name="updateOrderParameters">UpdateOrderParameters</param>
        /// <param name="restoredCart">Cart</param>
        /// <returns>
        /// A task that represents the asynchronous operation
        /// The task result contains the subtotal. Subtotal (incl tax). Subtotal tax rates. Discount amount (excl tax)
        /// </returns>
        protected virtual async Task<(decimal subtotal, decimal subTotalInclTax, SortedDictionary<decimal, decimal> subTotalTaxRates, decimal discountAmountExclTax)> UpdateSubTotalAsync(UpdateOrderParameters updateOrderParameters, IList<ShoppingCartItem> restoredCart)
        {
            var subTotalExclTax = decimal.Zero;
            var subTotalInclTax = decimal.Zero;
            var subTotalTaxRates = new SortedDictionary<decimal, decimal>();

            var updatedOrder = updateOrderParameters.UpdatedOrder;
            var updatedOrderItem = updateOrderParameters.UpdatedOrderItem;

            foreach (var shoppingCartItem in restoredCart)
            {
                decimal itemSubTotalExclTax;
                decimal itemSubTotalInclTax;
                decimal taxRate;

                //calculate subtotal for the updated order item
                if (shoppingCartItem.Id == updatedOrderItem.Id)
                {
                    //update order item 
                    updatedOrderItem.UnitPriceExclTax = updateOrderParameters.PriceExclTax;
                    updatedOrderItem.UnitPriceInclTax = updateOrderParameters.PriceInclTax;
                    updatedOrderItem.DiscountAmountExclTax = updateOrderParameters.DiscountAmountExclTax;
                    updatedOrderItem.DiscountAmountInclTax = updateOrderParameters.DiscountAmountInclTax;
                    updatedOrderItem.PriceExclTax = itemSubTotalExclTax = updateOrderParameters.SubTotalExclTax;
                    updatedOrderItem.PriceInclTax = itemSubTotalInclTax = updateOrderParameters.SubTotalInclTax;
                    updatedOrderItem.Quantity = shoppingCartItem.Quantity;

                    taxRate = itemSubTotalExclTax > 0 ? Math.Round(100 * (itemSubTotalInclTax - itemSubTotalExclTax) / itemSubTotalExclTax, 3) : 0M;
                }
                else
                {
                    //get the already calculated subtotal from the order item
                    var order = await OrderService.GetOrderItemByIdAsync(shoppingCartItem.Id);
                    itemSubTotalExclTax = order.PriceExclTax;
                    itemSubTotalInclTax = order.PriceInclTax;

                    taxRate = itemSubTotalExclTax > 0 ? Math.Round(100 * (itemSubTotalInclTax - itemSubTotalExclTax) / itemSubTotalExclTax, 3) : 0M;
                }

                subTotalExclTax += itemSubTotalExclTax;
                subTotalInclTax += itemSubTotalInclTax;

                //tax rates
                var itemTaxValue = itemSubTotalInclTax - itemSubTotalExclTax;
                if (taxRate <= decimal.Zero || itemTaxValue <= decimal.Zero)
                    continue;

                if (!subTotalTaxRates.ContainsKey(taxRate))
                    subTotalTaxRates.Add(taxRate, itemTaxValue);
                else
                    subTotalTaxRates[taxRate] = subTotalTaxRates[taxRate] + itemTaxValue;
            }

            if (subTotalExclTax < decimal.Zero)
                subTotalExclTax = decimal.Zero;

            if (subTotalInclTax < decimal.Zero)
                subTotalInclTax = decimal.Zero;

            //We calculate discount amount on order subtotal excl tax (discount first)
            //calculate discount amount ('Applied to order subtotal' discount)
            var customer = await CustomerService.GetCustomerByIdAsync(updatedOrder.CustomerId);
            var (discountAmountExclTax, subTotalDiscounts) = await GetOrderSubtotalDiscountAsync(customer, subTotalExclTax);
            if (subTotalExclTax < discountAmountExclTax)
                discountAmountExclTax = subTotalExclTax;
            var discountAmountInclTax = discountAmountExclTax;

            //add tax for shopping items
            var tempTaxRates = new Dictionary<decimal, decimal>(subTotalTaxRates);
            foreach (var kvp in tempTaxRates)
            {
                if (kvp.Value == decimal.Zero || subTotalExclTax <= decimal.Zero)
                    continue;

                var discountTaxValue = kvp.Value * (discountAmountExclTax / subTotalExclTax);
                discountAmountInclTax += discountTaxValue;
                subTotalTaxRates[kvp.Key] = kvp.Value - discountTaxValue;
            }

            //rounding
            if (ShoppingCartSettings.RoundPricesDuringCalculation)
            {
                subTotalExclTax = await PriceCalculationService.RoundPriceAsync(subTotalExclTax);
                subTotalInclTax = await PriceCalculationService.RoundPriceAsync(subTotalInclTax);
                discountAmountExclTax = await PriceCalculationService.RoundPriceAsync(discountAmountExclTax);
                discountAmountInclTax = await PriceCalculationService.RoundPriceAsync(discountAmountInclTax);
            }

            updatedOrder.OrderSubtotalExclTax = subTotalExclTax;
            updatedOrder.OrderSubtotalInclTax = subTotalInclTax;
            updatedOrder.OrderSubTotalDiscountExclTax = discountAmountExclTax;
            updatedOrder.OrderSubTotalDiscountInclTax = discountAmountInclTax;

            foreach (var discount in subTotalDiscounts)
                if (!DiscountService.ContainsDiscount(updateOrderParameters.AppliedDiscounts, discount))
                    updateOrderParameters.AppliedDiscounts.Add(discount);

            return (subTotalExclTax, subTotalInclTax, subTotalTaxRates, discountAmountExclTax);
        }

        /// <summary>
        /// Set reward points
        /// </summary>
        /// <param name="redeemedRewardPoints">Redeemed reward points</param>
        /// <param name="redeemedRewardPointsAmount">Redeemed reward points amount</param>
        /// <param name="useRewardPoints">A value indicating whether to use reward points</param>
        /// <param name="customer">Customer</param>
        /// <param name="orderTotal">Order total</param>
        /// <returns>A task that represents the asynchronous operation</returns>
        protected virtual async Task<(int redeemedRewardPoints, decimal redeemedRewardPointsAmount)> SetRewardPointsAsync(int redeemedRewardPoints, decimal redeemedRewardPointsAmount,
            bool? useRewardPoints, Customer customer, decimal orderTotal)
        {
            if (!RewardPointsSettings.Enabled)
                return (redeemedRewardPoints, redeemedRewardPointsAmount);

            var store = await StoreContext.GetCurrentStoreAsync();
            if (!useRewardPoints.HasValue)
                useRewardPoints = await GenericAttributeService.GetAttributeAsync<bool>(customer, NopCustomerDefaults.UseRewardPointsDuringCheckoutAttribute, store.Id);

            if (!useRewardPoints.Value)
                return (redeemedRewardPoints, redeemedRewardPointsAmount);

            var rewardPointsBalance = await RewardPointService.GetRewardPointsBalanceAsync(customer.Id, store.Id);
            rewardPointsBalance = RewardPointService.GetReducedPointsBalance(rewardPointsBalance);

            if (!CheckMinimumRewardPointsToUseRequirement(rewardPointsBalance))
                return (redeemedRewardPoints, redeemedRewardPointsAmount);

            var rewardPointsBalanceAmount = await ConvertRewardPointsToAmountAsync(rewardPointsBalance);

            if (orderTotal <= decimal.Zero)
                return (redeemedRewardPoints, redeemedRewardPointsAmount);

            if (orderTotal > rewardPointsBalanceAmount)
            {
                redeemedRewardPoints = rewardPointsBalance;
                redeemedRewardPointsAmount = rewardPointsBalanceAmount;
            }
            else
            {
                redeemedRewardPointsAmount = orderTotal;
                redeemedRewardPoints = ConvertAmountToRewardPoints(redeemedRewardPointsAmount);
            }

            return (redeemedRewardPoints, redeemedRewardPointsAmount);
        }

        /// <summary>
        /// Apply gift cards
        /// </summary>
        /// <param name="cart">Cart</param>
        /// <param name="appliedGiftCards">Applied gift cards</param>
        /// <param name="customer">Customer</param>
        /// <param name="resultTemp"></param>
        /// <returns>A task that represents the asynchronous operation</returns>
        protected virtual async Task<decimal> AppliedGiftCardsAsync(IList<ShoppingCartItem> cart, List<AppliedGiftCard> appliedGiftCards,
            Customer customer, decimal resultTemp)
        {
            if (await ShoppingCartService.ShoppingCartIsRecurringAsync(cart))
                return resultTemp;

            //we don't apply gift cards for recurring products
            var giftCards = await GiftCardService.GetActiveGiftCardsAppliedByCustomerAsync(customer);
            if (giftCards == null)
                return resultTemp;

            foreach (var gc in giftCards)
            {
                if (resultTemp <= decimal.Zero)
                    continue;

                var remainingAmount = await GiftCardService.GetGiftCardRemainingAmountAsync(gc);
                var amountCanBeUsed = resultTemp > remainingAmount ? remainingAmount : resultTemp;

                //reduce subtotal
                resultTemp -= amountCanBeUsed;

                var appliedGiftCard = new AppliedGiftCard
                {
                    GiftCard = gc,
                    AmountCanBeUsed = amountCanBeUsed
                };
                appliedGiftCards.Add(appliedGiftCard);
            }

            return resultTemp;
        }

        /// <summary>
        /// Gets shopping cart additional shipping charge
        /// </summary>
        /// <param name="cart">Cart</param>
        /// <returns>
        /// A task that represents the asynchronous operation
        /// The task result contains the additional shipping charge
        /// </returns>
        protected virtual async Task<decimal> GetShoppingCartAdditionalShippingChargeAsync(IList<ShoppingCartItem> cart)
        {
            return await cart.SumAwaitAsync(async shoppingCartItem => await ShippingService.GetAdditionalShippingChargeAsync(shoppingCartItem));
        }

        /// <summary>
        /// Converts an amount to reward points
        /// </summary>
        /// <param name="amount">Amount</param>
        /// <returns>Converted value</returns>
        protected virtual int ConvertAmountToRewardPoints(decimal amount)
        {
            var result = 0;
            if (amount <= 0)
                return 0;

            if (RewardPointsSettings.ExchangeRate > 0)
                result = (int)Math.Ceiling(amount / RewardPointsSettings.ExchangeRate);
            return result;
        }

        #endregion

        #region Methods

        /// <summary>
        /// Gets shopping cart subtotal
        /// </summary>
        /// <param name="cart">Cart</param>
        /// <param name="includingTax">A value indicating whether calculated price should include tax</param>
        /// <returns>
        /// A task that represents the asynchronous operation
        /// The task result contains the applied discount amount. Applied discounts. Sub total (without discount). Sub total (with discount). Tax rates (of order sub total)
        /// </returns>
        public virtual async Task<(decimal discountAmount, List<Discount> appliedDiscounts, decimal subTotalWithoutDiscount, decimal subTotalWithDiscount, SortedDictionary<decimal, decimal> taxRates)> GetShoppingCartSubTotalAsync(IList<ShoppingCartItem> cart,
            bool includingTax)
        {
            var discountAmount = decimal.Zero;
            var appliedDiscounts = new List<Discount>();
            var subTotalWithoutDiscount = decimal.Zero;
            var subTotalWithDiscount = decimal.Zero;
            var taxRates = new SortedDictionary<decimal, decimal>();

            if (!cart.Any())
                return (discountAmount, appliedDiscounts, subTotalWithoutDiscount, subTotalWithDiscount, taxRates);

            //get the customer 
            var customer = await CustomerService.GetShoppingCartCustomerAsync(cart);

            //sub totals
            var subTotalExclTaxWithoutDiscount = decimal.Zero;
            var subTotalInclTaxWithoutDiscount = decimal.Zero;
            foreach (var shoppingCartItem in cart)
            {
                var sciSubTotal = (await ShoppingCartService.GetSubTotalAsync(shoppingCartItem, true)).subTotal;
                var product = await ProductService.GetProductByIdAsync(shoppingCartItem.ProductId);

                var (sciExclTax, taxRate) = await TaxService.GetProductPriceAsync(product, sciSubTotal, false, customer);
                var (sciInclTax, _) = await TaxService.GetProductPriceAsync(product, sciSubTotal, true, customer);
                subTotalExclTaxWithoutDiscount += sciExclTax;
                subTotalInclTaxWithoutDiscount += sciInclTax;

                //tax rates
                var sciTax = sciInclTax - sciExclTax;
                if (taxRate <= decimal.Zero || sciTax <= decimal.Zero)
                    continue;

                if (!taxRates.ContainsKey(taxRate))
                {
                    taxRates.Add(taxRate, sciTax);
                }
                else
                {
                    taxRates[taxRate] = taxRates[taxRate] + sciTax;
                }
            }

            //checkout attributes
            if (customer != null)
            {
                var store = await StoreContext.GetCurrentStoreAsync();
                var checkoutAttributesXml = await GenericAttributeService.GetAttributeAsync<string>(customer, NopCustomerDefaults.CheckoutAttributes, store.Id);
                var attributeValues = CheckoutAttributeParser.ParseCheckoutAttributeValues(checkoutAttributesXml);
                if (attributeValues != null)
                {
                    await foreach (var (attribute, values) in attributeValues)
                    {
                        await foreach (var attributeValue in values)
                        {
                            var (caExclTax, taxRate) = await TaxService.GetCheckoutAttributePriceAsync(attribute, attributeValue, false, customer);
                            var (caInclTax, _) = await TaxService.GetCheckoutAttributePriceAsync(attribute, attributeValue, true, customer);

                            subTotalExclTaxWithoutDiscount += caExclTax;
                            subTotalInclTaxWithoutDiscount += caInclTax;

                            //tax rates
                            var caTax = caInclTax - caExclTax;
                            if (taxRate <= decimal.Zero || caTax <= decimal.Zero)
                                continue;

                            if (!taxRates.ContainsKey(taxRate))
                            {
                                taxRates.Add(taxRate, caTax);
                            }
                            else
                            {
                                taxRates[taxRate] = taxRates[taxRate] + caTax;
                            }
                        }
                    }
                }
            }

            //subtotal without discount
            subTotalWithoutDiscount = includingTax ? subTotalInclTaxWithoutDiscount : subTotalExclTaxWithoutDiscount;
            if (subTotalWithoutDiscount < decimal.Zero)
                subTotalWithoutDiscount = decimal.Zero;

            if (ShoppingCartSettings.RoundPricesDuringCalculation)
                subTotalWithoutDiscount = await PriceCalculationService.RoundPriceAsync(subTotalWithoutDiscount);

            //We calculate discount amount on order subtotal excl tax (discount first)
            //calculate discount amount ('Applied to order subtotal' discount)
            decimal discountAmountExclTax;
            (discountAmountExclTax, appliedDiscounts) = await GetOrderSubtotalDiscountAsync(customer, subTotalExclTaxWithoutDiscount);
            if (subTotalExclTaxWithoutDiscount < discountAmountExclTax)
                discountAmountExclTax = subTotalExclTaxWithoutDiscount;
            var discountAmountInclTax = discountAmountExclTax;
            //subtotal with discount (excl tax)
            var subTotalExclTaxWithDiscount = subTotalExclTaxWithoutDiscount - discountAmountExclTax;
            var subTotalInclTaxWithDiscount = subTotalExclTaxWithDiscount;

            //add tax for shopping items & checkout attributes
            var tempTaxRates = new Dictionary<decimal, decimal>(taxRates);
            foreach (var kvp in tempTaxRates)
            {
                var taxRate = kvp.Key;
                var taxValue = kvp.Value;

                if (taxValue == decimal.Zero)
                    continue;

                //discount the tax amount that applies to subtotal items
                if (subTotalExclTaxWithoutDiscount > decimal.Zero)
                {
                    var discountTax = taxRates[taxRate] * (discountAmountExclTax / subTotalExclTaxWithoutDiscount);
                    discountAmountInclTax += discountTax;
                    taxValue = taxRates[taxRate] - discountTax;
                    if (ShoppingCartSettings.RoundPricesDuringCalculation)
                        taxValue = await PriceCalculationService.RoundPriceAsync(taxValue);
                    taxRates[taxRate] = taxValue;
                }

                //subtotal with discount (incl tax)
                subTotalInclTaxWithDiscount += taxValue;
            }

            if (ShoppingCartSettings.RoundPricesDuringCalculation)
            {
                discountAmountInclTax = await PriceCalculationService.RoundPriceAsync(discountAmountInclTax);
                discountAmountExclTax = await PriceCalculationService.RoundPriceAsync(discountAmountExclTax);
            }

            if (includingTax)
            {
                subTotalWithDiscount = subTotalInclTaxWithDiscount;
                discountAmount = discountAmountInclTax;
            }
            else
            {
                subTotalWithDiscount = subTotalExclTaxWithDiscount;
                discountAmount = discountAmountExclTax;
            }

            if (subTotalWithDiscount < decimal.Zero)
                subTotalWithDiscount = decimal.Zero;

            if (ShoppingCartSettings.RoundPricesDuringCalculation)
                subTotalWithDiscount = await PriceCalculationService.RoundPriceAsync(subTotalWithDiscount);

            return (discountAmount, appliedDiscounts, subTotalWithoutDiscount, subTotalWithDiscount, taxRates);
        }

        /// <summary>
        /// Update order totals
        /// </summary>
        /// <param name="updateOrderParameters">Parameters for the updating order</param>
        /// <param name="restoredCart">Shopping cart</param>
        /// <returns>A task that represents the asynchronous operation</returns>
        public virtual async Task UpdateOrderTotalsAsync(UpdateOrderParameters updateOrderParameters, IList<ShoppingCartItem> restoredCart)
        {
            //sub total
            var (subTotalExclTax, subTotalInclTax, subTotalTaxRates, discountAmountExclTax) = await UpdateSubTotalAsync(updateOrderParameters, restoredCart);

            //shipping
            var (shippingTotalExclTax, shippingTotalInclTax, shippingTaxRate) = await UpdateShippingAsync(updateOrderParameters, restoredCart, subTotalInclTax, subTotalExclTax);

            //tax rates
            var taxTotal = await UpdateTaxRatesAsync(subTotalTaxRates, shippingTotalInclTax, shippingTotalExclTax, shippingTaxRate, updateOrderParameters.UpdatedOrder);

            //total
            await UpdateTotalAsync(updateOrderParameters, subTotalExclTax, discountAmountExclTax, shippingTotalExclTax, taxTotal);
        }
        
        /// <summary>
        /// Gets a value indicating whether shipping is free
        /// </summary>
        /// <param name="cart">Cart</param>
        /// <param name="subTotal">Subtotal amount; pass null to calculate subtotal</param>
        /// <returns>
        /// A task that represents the asynchronous operation
        /// The task result contains a value indicating whether shipping is free
        /// </returns>
        public virtual async Task<bool> IsFreeShippingAsync(IList<ShoppingCartItem> cart, decimal? subTotal = null)
        {
            //check whether customer is in a customer role with free shipping applied
            var customer = await CustomerService.GetCustomerByIdAsync(cart.FirstOrDefault()?.CustomerId ?? 0);

            if (customer != null && (await CustomerService.GetCustomerRolesAsync(customer)).Any(role => role.FreeShipping))
                return true;

            //check whether all shopping cart items and their associated products marked as free shipping
            if (await cart.AllAwaitAsync(async shoppingCartItem => await ShippingService.IsFreeShippingAsync(shoppingCartItem)))
                return true;

            //free shipping over $X
            if (!ShippingSettings.FreeShippingOverXEnabled)
                return false;

            if (!subTotal.HasValue)
            {
                var (_, _, _, subTotalWithDiscount, _) = await GetShoppingCartSubTotalAsync(cart, ShippingSettings.FreeShippingOverXIncludingTax);
                subTotal = subTotalWithDiscount;
            }

            //check whether we have subtotal enough to have free shipping
            if (subTotal.Value > ShippingSettings.FreeShippingOverXValue)
                return true;

            return false;
        }

        /// <summary>
        /// Adjust shipping rate (free shipping, additional charges, discounts)
        /// </summary>
        /// <param name="shippingRate">Shipping rate to adjust</param>
        /// <param name="cart">Cart</param>
        /// <param name="applyToPickupInStore">Adjust shipping rate to pickup in store shipping option rate</param>
        /// <returns>
        /// A task that represents the asynchronous operation
        /// The task result contains the adjusted shipping rate. Applied discounts
        /// </returns>
        public virtual async Task<(decimal adjustedShippingRate, List<Discount> appliedDiscounts)> AdjustShippingRateAsync(decimal shippingRate, IList<ShoppingCartItem> cart, 
            bool applyToPickupInStore = false)
        {
            //free shipping
            if (await IsFreeShippingAsync(cart))
                return (decimal.Zero, new List<Discount>());

            var customer = await CustomerService.GetShoppingCartCustomerAsync(cart);
            var store = await StoreContext.GetCurrentStoreAsync();

            //with additional shipping charges
            var pickupPoint = await GenericAttributeService.GetAttributeAsync<PickupPoint>(customer,
                    NopCustomerDefaults.SelectedPickupPointAttribute, store.Id);

            var adjustedRate = shippingRate;

            if (!(applyToPickupInStore && ShippingSettings.AllowPickupInStore && pickupPoint != null && ShippingSettings.IgnoreAdditionalShippingChargeForPickupInStore))
            {
                adjustedRate += await GetShoppingCartAdditionalShippingChargeAsync(cart);
            }

            //discount
            var (discountAmount, appliedDiscounts) = await GetShippingDiscountAsync(customer, adjustedRate);
            adjustedRate -= discountAmount;

            adjustedRate = Math.Max(adjustedRate, decimal.Zero);
            if (ShoppingCartSettings.RoundPricesDuringCalculation)
                adjustedRate = await PriceCalculationService.RoundPriceAsync(adjustedRate);

            return (adjustedRate, appliedDiscounts);
        }

        /// <summary>
        /// Gets shopping cart shipping total
        /// </summary>
        /// <param name="cart">Cart</param>
        /// <returns>
        /// A task that represents the asynchronous operation
        /// The task result contains the shipping total
        /// </returns>
        public virtual async Task<decimal?> GetShoppingCartShippingTotalAsync(IList<ShoppingCartItem> cart)
        {
            var includingTax = await WorkContext.GetTaxDisplayTypeAsync() == TaxDisplayType.IncludingTax;
            return (await GetShoppingCartShippingTotalAsync(cart, includingTax)).shippingTotal;
        }

        
        /// <summary>
        /// Gets shopping cart shipping total
        /// </summary>
        /// <param name="cart">Cart</param>
        /// <param name="includingTax">A value indicating whether calculated price should include tax</param>
        /// <returns>
        /// A task that represents the asynchronous operation
        /// The task result contains the shipping total. Applied tax rate. Applied discounts
        /// </returns>
        public virtual async Task<(decimal? shippingTotal, decimal taxRate, List<Discount> appliedDiscounts)> GetShoppingCartShippingTotalAsync(IList<ShoppingCartItem> cart, bool includingTax)
        {
            decimal? shippingTotal = null;
            var appliedDiscounts = new List<Discount>();
            var taxRate = decimal.Zero;

            var customer = await CustomerService.GetShoppingCartCustomerAsync(cart);

            var isFreeShipping = await IsFreeShippingAsync(cart);
            if (isFreeShipping)
                return (decimal.Zero, taxRate, appliedDiscounts);

            ShippingOption shippingOption = null;
            var store = await StoreContext.GetCurrentStoreAsync();
            if (customer != null)
                shippingOption = await GenericAttributeService.GetAttributeAsync<ShippingOption>(customer, NopCustomerDefaults.SelectedShippingOptionAttribute, store.Id);

            if (shippingOption != null)
            {
                //use last shipping option (get from cache)
                (shippingTotal, appliedDiscounts) = await AdjustShippingRateAsync(shippingOption.Rate, cart, shippingOption.IsPickupInStore);
            }
            else
            {
                //use fixed rate (if possible)
                Address shippingAddress = null;
                if (customer != null)
                    shippingAddress = await CustomerService.GetCustomerShippingAddressAsync(customer);

                var shippingRateComputationMethods = await ShippingPluginManager.LoadActivePluginsAsync(await WorkContext.GetCurrentCustomerAsync(), store.Id);
                if (!shippingRateComputationMethods.Any() && !ShippingSettings.AllowPickupInStore)
                    throw new NopException("Shipping rate computation method could not be loaded");

                if (shippingRateComputationMethods.Count == 1)
                {
                    var shippingRateComputationMethod = shippingRateComputationMethods[0];

                    var shippingOptionRequests = (await ShippingService.CreateShippingOptionRequestsAsync(cart,
                        shippingAddress,
                        store.Id)).shipmentPackages;

                    decimal? fixedRate = null;
                    foreach (var shippingOptionRequest in shippingOptionRequests)
                    {
                        //calculate fixed rates for each request-package
                        var fixedRateTmp = await shippingRateComputationMethod.GetFixedRateAsync(shippingOptionRequest);
                        if (!fixedRateTmp.HasValue)
                            continue;

                        if (!fixedRate.HasValue)
                            fixedRate = decimal.Zero;

                        fixedRate += fixedRateTmp.Value;
                    }

                    if (fixedRate.HasValue)
                    {
                        //adjust shipping rate
                        (shippingTotal, appliedDiscounts) = await AdjustShippingRateAsync(fixedRate.Value, cart);
                    }
                }
            }

            if (!shippingTotal.HasValue)
                return (null, taxRate, appliedDiscounts);

            if (shippingTotal.Value < decimal.Zero)
                shippingTotal = decimal.Zero;

            //round
            if (ShoppingCartSettings.RoundPricesDuringCalculation)
                shippingTotal = await PriceCalculationService.RoundPriceAsync(shippingTotal.Value);

            decimal? shippingTotalTaxed;

            (shippingTotalTaxed, taxRate) = await TaxService.GetShippingPriceAsync(shippingTotal.Value,
                includingTax,
                customer);

            //round
            if (ShoppingCartSettings.RoundPricesDuringCalculation)
                shippingTotalTaxed = await PriceCalculationService.RoundPriceAsync(shippingTotalTaxed.Value);

            return (shippingTotalTaxed, taxRate, appliedDiscounts);
        }
        
        /// <summary>
        /// Gets tax
        /// </summary>
        /// <param name="cart">Shopping cart</param>
        /// <param name="usePaymentMethodAdditionalFee">A value indicating whether we should use payment method additional fee when calculating tax</param>
        /// <returns>
        /// A task that represents the asynchronous operation
        /// The task result contains the ax total, Tax rates
        /// </returns>
        public virtual async Task<(decimal taxTotal, SortedDictionary<decimal, decimal> taxRates)> GetTaxTotalAsync(IList<ShoppingCartItem> cart, bool usePaymentMethodAdditionalFee = true)
        {
            if (cart == null)
                throw new ArgumentNullException(nameof(cart));

            var taxTotalResult = await TaxService.GetTaxTotalAsync(cart, usePaymentMethodAdditionalFee);
            var taxRates = taxTotalResult?.TaxRates ?? new SortedDictionary<decimal, decimal>();
            var taxTotal = taxTotalResult?.TaxTotal ?? decimal.Zero;

            if (ShoppingCartSettings.RoundPricesDuringCalculation)
                taxTotal = await PriceCalculationService.RoundPriceAsync(taxTotal);

            return (taxTotal, taxRates);
        }
        
        /// <summary>
        /// Gets shopping cart total
        /// </summary>
        /// <param name="cart">Cart</param>
        /// <param name="useRewardPoints">A value indicating reward points should be used; null to detect current choice of the customer</param>
        /// <param name="usePaymentMethodAdditionalFee">A value indicating whether we should use payment method additional fee when calculating order total</param>
        /// <returns>
        /// A task that represents the asynchronous operation
        /// The task result contains the shopping cart total;Null if shopping cart total couldn't be calculated now. Applied gift cards. Applied discount amount. Applied discounts. Reward points to redeem. Reward points amount in primary store currency to redeem
        /// </returns>
        public virtual async Task<(decimal? shoppingCartTotal, decimal discountAmount, List<Discount> appliedDiscounts, List<AppliedGiftCard> appliedGiftCards, int redeemedRewardPoints, decimal redeemedRewardPointsAmount)> GetShoppingCartTotalAsync(IList<ShoppingCartItem> cart,
            bool? useRewardPoints = null, bool usePaymentMethodAdditionalFee = true)
        {
            var redeemedRewardPoints = 0;
            var redeemedRewardPointsAmount = decimal.Zero;

            var customer = await CustomerService.GetShoppingCartCustomerAsync(cart);
            var store = await StoreContext.GetCurrentStoreAsync();
            var paymentMethodSystemName = string.Empty;

            if (customer != null)
            {
                paymentMethodSystemName = await GenericAttributeService.GetAttributeAsync<string>(customer,
                    NopCustomerDefaults.SelectedPaymentMethodAttribute, store.Id);
            }

            //subtotal without tax
            var (_, _, _, subTotalWithDiscountBase, _) = await GetShoppingCartSubTotalAsync(cart, false);
            //subtotal with discount
            var subtotalBase = subTotalWithDiscountBase;

            //shipping without tax
            var shoppingCartShipping = (await GetShoppingCartShippingTotalAsync(cart, false)).shippingTotal;

            //payment method additional fee without tax
            var paymentMethodAdditionalFeeWithoutTax = decimal.Zero;
            if (usePaymentMethodAdditionalFee && !string.IsNullOrEmpty(paymentMethodSystemName))
            {
                var paymentMethodAdditionalFee = await PaymentService.GetAdditionalHandlingFeeAsync(cart,
                    paymentMethodSystemName);
                paymentMethodAdditionalFeeWithoutTax =
                    (await TaxService.GetPaymentMethodAdditionalFeeAsync(paymentMethodAdditionalFee,
                        false, customer)).price;
            }

            //tax
            var shoppingCartTax = (await GetTaxTotalAsync(cart, usePaymentMethodAdditionalFee)).taxTotal;

            //order total
            var resultTemp = decimal.Zero;
            resultTemp += subtotalBase;
            if (shoppingCartShipping.HasValue)
            {
                resultTemp += shoppingCartShipping.Value;
            }

            resultTemp += paymentMethodAdditionalFeeWithoutTax;
            resultTemp += shoppingCartTax;
            if (ShoppingCartSettings.RoundPricesDuringCalculation)
                resultTemp = await PriceCalculationService.RoundPriceAsync(resultTemp);

            //order total discount
            var (discountAmount, appliedDiscounts) = await GetOrderTotalDiscountAsync(customer, resultTemp);

            //sub totals with discount        
            if (resultTemp < discountAmount)
                discountAmount = resultTemp;

            //reduce subtotal
            resultTemp -= discountAmount;

            if (resultTemp < decimal.Zero)
                resultTemp = decimal.Zero;
            if (ShoppingCartSettings.RoundPricesDuringCalculation)
                resultTemp = await PriceCalculationService.RoundPriceAsync(resultTemp);

            //let's apply gift cards now (gift cards that can be used)
            var appliedGiftCards = new List<AppliedGiftCard>();
            resultTemp = await AppliedGiftCardsAsync(cart, appliedGiftCards, customer, resultTemp);

            if (resultTemp < decimal.Zero)
                resultTemp = decimal.Zero;
            if (ShoppingCartSettings.RoundPricesDuringCalculation)
                resultTemp = await PriceCalculationService.RoundPriceAsync(resultTemp);

            if (!shoppingCartShipping.HasValue)
            {
                //we have errors
                return (null, discountAmount, appliedDiscounts, appliedGiftCards,redeemedRewardPoints, redeemedRewardPointsAmount);
            }

            var orderTotal = resultTemp;

            //reward points
            (redeemedRewardPoints, redeemedRewardPointsAmount) = await SetRewardPointsAsync(redeemedRewardPoints, redeemedRewardPointsAmount, useRewardPoints, customer, orderTotal);

            orderTotal -= redeemedRewardPointsAmount;

            if (ShoppingCartSettings.RoundPricesDuringCalculation)
                orderTotal = await PriceCalculationService.RoundPriceAsync(orderTotal);
            return (orderTotal, discountAmount, appliedDiscounts, appliedGiftCards, redeemedRewardPoints, redeemedRewardPointsAmount);
        }

        /// <summary>
        /// Converts existing reward points to amount
        /// </summary>
        /// <param name="rewardPoints">Reward points</param>
        /// <returns>
        /// A task that represents the asynchronous operation
        /// The task result contains the converted value
        /// </returns>
        public virtual async Task<decimal> ConvertRewardPointsToAmountAsync(int rewardPoints)
        {
            if (rewardPoints <= 0)
                return decimal.Zero;

            var result = rewardPoints * RewardPointsSettings.ExchangeRate;
            if (ShoppingCartSettings.RoundPricesDuringCalculation)
                result = await PriceCalculationService.RoundPriceAsync(result);
            return result;
        }

        /// <summary>
        /// Gets a value indicating whether a customer has minimum amount of reward points to use (if enabled)
        /// </summary>
        /// <param name="rewardPoints">Reward points to check</param>
        /// <returns>true - reward points could use; false - cannot be used.</returns>
        public virtual bool CheckMinimumRewardPointsToUseRequirement(int rewardPoints)
        {
            if (RewardPointsSettings.MinimumRewardPointsToUse <= 0)
                return true;

            return rewardPoints >= RewardPointsSettings.MinimumRewardPointsToUse;
        }

        /// <summary>
        /// Calculate how order total (maximum amount) for which reward points could be earned/reduced
        /// </summary>
        /// <param name="orderShippingInclTax">Order shipping (including tax)</param>
        /// <param name="orderTotal">Order total</param>
        /// <returns>Applicable order total</returns>
        public virtual decimal CalculateApplicableOrderTotalForRewardPoints(decimal orderShippingInclTax, decimal orderTotal)
        {
            //do you give reward points for order total? or do you exclude shipping?
            //since shipping costs vary some of store owners don't give reward points based on shipping total
            //you can put your custom logic here
            var totalForRewardPoints = orderTotal - orderShippingInclTax;

            //check the minimum total to award points
            if (totalForRewardPoints < RewardPointsSettings.MinOrderTotalToAwardPoints)
                return decimal.Zero;

            return totalForRewardPoints;
        }

        /// <summary>
        /// Calculate how much reward points will be earned/reduced based on certain amount spent
        /// </summary>
        /// <param name="customer">Customer</param>
        /// <param name="amount">Amount (in primary store currency)</param>
        /// <returns>
        /// A task that represents the asynchronous operation
        /// The task result contains the number of reward points
        /// </returns>
        public virtual async Task<int> CalculateRewardPointsAsync(Customer customer, decimal amount)
        {
            if (!RewardPointsSettings.Enabled)
                return 0;

            if (RewardPointsSettings.PointsForPurchases_Amount <= decimal.Zero)
                return 0;

            //ensure that reward points are applied only to registered users
            if (customer == null || await CustomerService.IsGuestAsync(customer))
                return 0;

            var points = (int)Math.Truncate(amount / RewardPointsSettings.PointsForPurchases_Amount) * RewardPointsSettings.PointsForPurchases_Points;
            return points;
        }

        #endregion
    }
}