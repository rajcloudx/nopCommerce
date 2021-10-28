﻿using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Nop.Core.Domain.Shipping;
using Nop.Data;

namespace Nop.Services.Shipping.Date
{
    /// <summary>
    /// Represents the date range service
    /// </summary>
    public partial class DateRangeService : IDateRangeService
    {
        #region Fields

        protected IRepository<DeliveryDate> DeliveryDateRepository { get; }
        protected IRepository<ProductAvailabilityRange> ProductAvailabilityRangeRepository { get; }

        #endregion

        #region Ctor

        public DateRangeService(IRepository<DeliveryDate> deliveryDateRepository,
            IRepository<ProductAvailabilityRange> productAvailabilityRangeRepository)
        {
            DeliveryDateRepository = deliveryDateRepository;
            ProductAvailabilityRangeRepository = productAvailabilityRangeRepository;
        }

        #endregion

        #region Methods

        #region Delivery dates

        /// <summary>
        /// Get a delivery date
        /// </summary>
        /// <param name="deliveryDateId">The delivery date identifier</param>
        /// <returns>
        /// A task that represents the asynchronous operation
        /// The task result contains the delivery date
        /// </returns>
        public virtual async Task<DeliveryDate> GetDeliveryDateByIdAsync(int deliveryDateId)
        {
            return await DeliveryDateRepository.GetByIdAsync(deliveryDateId, cache => default);
        }

        /// <summary>
        /// Get all delivery dates
        /// </summary>
        /// <returns>
        /// A task that represents the asynchronous operation
        /// The task result contains the delivery dates
        /// </returns>
        public virtual async Task<IList<DeliveryDate>> GetAllDeliveryDatesAsync()
        {
            var deliveryDates = await DeliveryDateRepository.GetAllAsync(query =>
            {
                return from dd in query
                    orderby dd.DisplayOrder, dd.Id
                    select dd;
            }, cache => default);

            return deliveryDates;
        }

        /// <summary>
        /// Insert a delivery date
        /// </summary>
        /// <param name="deliveryDate">Delivery date</param>
        /// <returns>A task that represents the asynchronous operation</returns>
        public virtual async Task InsertDeliveryDateAsync(DeliveryDate deliveryDate)
        {
            await DeliveryDateRepository.InsertAsync(deliveryDate);
        }

        /// <summary>
        /// Update the delivery date
        /// </summary>
        /// <param name="deliveryDate">Delivery date</param>
        /// <returns>A task that represents the asynchronous operation</returns>
        public virtual async Task UpdateDeliveryDateAsync(DeliveryDate deliveryDate)
        {
            await DeliveryDateRepository.UpdateAsync(deliveryDate);
        }

        /// <summary>
        /// Delete a delivery date
        /// </summary>
        /// <param name="deliveryDate">The delivery date</param>
        /// <returns>A task that represents the asynchronous operation</returns>
        public virtual async Task DeleteDeliveryDateAsync(DeliveryDate deliveryDate)
        {
            await DeliveryDateRepository.DeleteAsync(deliveryDate);
        }

        #endregion

        #region Product availability ranges

        /// <summary>
        /// Get a product availability range
        /// </summary>
        /// <param name="productAvailabilityRangeId">The product availability range identifier</param>
        /// <returns>
        /// A task that represents the asynchronous operation
        /// The task result contains the product availability range
        /// </returns>
        public virtual async Task<ProductAvailabilityRange> GetProductAvailabilityRangeByIdAsync(int productAvailabilityRangeId)
        {
            return productAvailabilityRangeId != 0 ? await ProductAvailabilityRangeRepository.GetByIdAsync(productAvailabilityRangeId, cache => default) : null;
        }

        /// <summary>
        /// Get all product availability ranges
        /// </summary>
        /// <returns>
        /// A task that represents the asynchronous operation
        /// The task result contains the product availability ranges
        /// </returns>
        public virtual async Task<IList<ProductAvailabilityRange>> GetAllProductAvailabilityRangesAsync()
        {
            return await ProductAvailabilityRangeRepository.GetAllAsync(query =>
            {
                return from par in query
                    orderby par.DisplayOrder, par.Id
                    select par;
            }, cache => default);
        }

        /// <summary>
        /// Insert the product availability range
        /// </summary>
        /// <param name="productAvailabilityRange">Product availability range</param>
        /// <returns>A task that represents the asynchronous operation</returns>
        public virtual async Task InsertProductAvailabilityRangeAsync(ProductAvailabilityRange productAvailabilityRange)
        {
            await ProductAvailabilityRangeRepository.InsertAsync(productAvailabilityRange);
        }

        /// <summary>
        /// Update the product availability range
        /// </summary>
        /// <param name="productAvailabilityRange">Product availability range</param>
        /// <returns>A task that represents the asynchronous operation</returns>
        public virtual async Task UpdateProductAvailabilityRangeAsync(ProductAvailabilityRange productAvailabilityRange)
        {
            await ProductAvailabilityRangeRepository.UpdateAsync(productAvailabilityRange);
        }

        /// <summary>
        /// Delete the product availability range
        /// </summary>
        /// <param name="productAvailabilityRange">Product availability range</param>
        /// <returns>A task that represents the asynchronous operation</returns>
        public virtual async Task DeleteProductAvailabilityRangeAsync(ProductAvailabilityRange productAvailabilityRange)
        {
            await ProductAvailabilityRangeRepository.DeleteAsync(productAvailabilityRange);
        }

        #endregion

        #endregion
    }
}