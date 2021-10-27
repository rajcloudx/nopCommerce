﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Nop.Core;
using Nop.Core.Domain.Directory;
using Nop.Data;
using Nop.Services.Stores;

namespace Nop.Services.Directory
{
    /// <summary>
    /// Currency service
    /// </summary>
    public partial class CurrencyService : ICurrencyService
    {
        #region Fields

        protected CurrencySettings CurrencySettings { get; }
        protected IExchangeRatePluginManager ExchangeRatePluginManager { get; }
        protected IRepository<Currency> CurrencyRepository { get; }
        protected IStoreMappingService StoreMappingService { get; }

        #endregion

        #region Ctor

        public CurrencyService(CurrencySettings currencySettings,
            IExchangeRatePluginManager exchangeRatePluginManager,
            IRepository<Currency> currencyRepository,
            IStoreMappingService storeMappingService)
        {
            CurrencySettings = currencySettings;
            ExchangeRatePluginManager = exchangeRatePluginManager;
            CurrencyRepository = currencyRepository;
            StoreMappingService = storeMappingService;
        }

        #endregion
        
        #region Methods

        #region Currency

        /// <summary>
        /// Deletes currency
        /// </summary>
        /// <param name="currency">Currency</param>
        /// <returns>A task that represents the asynchronous operation</returns>
        public virtual async Task DeleteCurrencyAsync(Currency currency)
        {
            await CurrencyRepository.DeleteAsync(currency);
        }

        /// <summary>
        /// Gets a currency
        /// </summary>
        /// <param name="currencyId">Currency identifier</param>
        /// <returns>
        /// A task that represents the asynchronous operation
        /// The task result contains the currency
        /// </returns>
        public virtual async Task<Currency> GetCurrencyByIdAsync(int currencyId)
        {
            return await CurrencyRepository.GetByIdAsync(currencyId, cache => default);
        }

        /// <summary>
        /// Gets a currency by code
        /// </summary>
        /// <param name="currencyCode">Currency code</param>
        /// <returns>
        /// A task that represents the asynchronous operation
        /// The task result contains the currency
        /// </returns>
        public virtual async Task<Currency> GetCurrencyByCodeAsync(string currencyCode)
        {
            if (string.IsNullOrEmpty(currencyCode))
                return null;

            return (await GetAllCurrenciesAsync(true))
                .FirstOrDefault(c => c.CurrencyCode.ToLowerInvariant() == currencyCode.ToLowerInvariant());
        }

        /// <summary>
        /// Gets all currencies
        /// </summary>
        /// <param name="showHidden">A value indicating whether to show hidden records</param>
        /// <param name="storeId">Load records allowed only in a specified store; pass 0 to load all records</param>
        /// <returns>
        /// A task that represents the asynchronous operation
        /// The task result contains the currencies
        /// </returns>
        public virtual async Task<IList<Currency>> GetAllCurrenciesAsync(bool showHidden = false, int storeId = 0)
        {
            var currencies = await CurrencyRepository.GetAllAsync(query =>
            {
                if (!showHidden)
                    query = query.Where(c => c.Published);

                query = query.OrderBy(c => c.DisplayOrder).ThenBy(c => c.Id);

                return query;
            }, cache => cache.PrepareKeyForDefaultCache(NopDirectoryDefaults.CurrenciesAllCacheKey, showHidden));

            //store mapping
            if (storeId > 0)
                currencies = await currencies
                    .WhereAwait(async c => await StoreMappingService.AuthorizeAsync(c, storeId))
                    .ToListAsync();

            return currencies;
        }

        /// <summary>
        /// Inserts a currency
        /// </summary>
        /// <param name="currency">Currency</param>
        /// <returns>A task that represents the asynchronous operation</returns>
        public virtual async Task InsertCurrencyAsync(Currency currency)
        {
            await CurrencyRepository.InsertAsync(currency);
        }

        /// <summary>
        /// Updates the currency
        /// </summary>
        /// <param name="currency">Currency</param>
        /// <returns>A task that represents the asynchronous operation</returns>
        public virtual async Task UpdateCurrencyAsync(Currency currency)
        {
            await CurrencyRepository.UpdateAsync(currency);
        }

        #endregion

        #region Conversions

        /// <summary>
        /// Gets live rates regarding the passed currency
        /// </summary>
        /// <param name="currencyCode">Currency code; pass null to use primary exchange rate currency</param>
        /// <returns>
        /// A task that represents the asynchronous operation
        /// The task result contains the exchange rates
        /// </returns>
        public virtual async Task<IList<ExchangeRate>> GetCurrencyLiveRatesAsync(string currencyCode = null)
        {
            var exchangeRateProvider = await ExchangeRatePluginManager.LoadPrimaryPluginAsync()
                ?? throw new Exception("Active exchange rate provider cannot be loaded");

            currencyCode ??= (await GetCurrencyByIdAsync(CurrencySettings.PrimaryExchangeRateCurrencyId))?.CurrencyCode
                ?? throw new NopException("Primary exchange rate currency is not set");

            return await exchangeRateProvider.GetCurrencyLiveRatesAsync(currencyCode);
        }

        /// <summary>
        /// Converts currency
        /// </summary>
        /// <param name="amount">Amount</param>
        /// <param name="exchangeRate">Currency exchange rate</param>
        /// <returns>Converted value</returns>
        public virtual decimal ConvertCurrency(decimal amount, decimal exchangeRate)
        {
            if (amount != decimal.Zero && exchangeRate != decimal.Zero)
                return amount * exchangeRate;

            return decimal.Zero;
        }

        /// <summary>
        /// Converts to primary store currency 
        /// </summary>
        /// <param name="amount">Amount</param>
        /// <param name="sourceCurrencyCode">Source currency code</param>
        /// <returns>
        /// A task that represents the asynchronous operation
        /// The task result contains the converted value
        /// </returns>
        public virtual async Task<decimal> ConvertToPrimaryStoreCurrencyAsync(decimal amount, Currency sourceCurrencyCode)
        {
            if (sourceCurrencyCode == null)
                throw new ArgumentNullException(nameof(sourceCurrencyCode));

            var primaryStoreCurrency = await GetCurrencyByIdAsync(CurrencySettings.PrimaryStoreCurrencyId);
            var result = await ConvertCurrencyAsync(amount, sourceCurrencyCode, primaryStoreCurrency);
            
            return result;
        }

        /// <summary>
        /// Converts from primary store currency
        /// </summary>
        /// <param name="amount">Amount</param>
        /// <param name="targetCurrencyCode">Target currency code</param>
        /// <returns>
        /// A task that represents the asynchronous operation
        /// The task result contains the converted value
        /// </returns>
        public virtual async Task<decimal> ConvertFromPrimaryStoreCurrencyAsync(decimal amount, Currency targetCurrencyCode)
        {
            var primaryStoreCurrency = await GetCurrencyByIdAsync(CurrencySettings.PrimaryStoreCurrencyId);
            var result = await ConvertCurrencyAsync(amount, primaryStoreCurrency, targetCurrencyCode);
            
            return result;
        }

        /// <summary>
        /// Converts currency
        /// </summary>
        /// <param name="amount">Amount</param>
        /// <param name="sourceCurrencyCode">Source currency code</param>
        /// <param name="targetCurrencyCode">Target currency code</param>
        /// <returns>
        /// A task that represents the asynchronous operation
        /// The task result contains the converted value
        /// </returns>
        public virtual async Task<decimal> ConvertCurrencyAsync(decimal amount, Currency sourceCurrencyCode, Currency targetCurrencyCode)
        {
            if (sourceCurrencyCode == null)
                throw new ArgumentNullException(nameof(sourceCurrencyCode));

            if (targetCurrencyCode == null)
                throw new ArgumentNullException(nameof(targetCurrencyCode));

            var result = amount;

            if (result == decimal.Zero || sourceCurrencyCode.Id == targetCurrencyCode.Id)
                return result;

            result = await ConvertToPrimaryExchangeRateCurrencyAsync(result, sourceCurrencyCode);
            result = await ConvertFromPrimaryExchangeRateCurrencyAsync(result, targetCurrencyCode);
            return result;
        }

        /// <summary>
        /// Converts to primary exchange rate currency 
        /// </summary>
        /// <param name="amount">Amount</param>
        /// <param name="sourceCurrencyCode">Source currency code</param>
        /// <returns>
        /// A task that represents the asynchronous operation
        /// The task result contains the converted value
        /// </returns>
        public virtual async Task<decimal> ConvertToPrimaryExchangeRateCurrencyAsync(decimal amount, Currency sourceCurrencyCode)
        {
            if (sourceCurrencyCode == null)
                throw new ArgumentNullException(nameof(sourceCurrencyCode));

            var primaryExchangeRateCurrency = await GetCurrencyByIdAsync(CurrencySettings.PrimaryExchangeRateCurrencyId);
            if (primaryExchangeRateCurrency == null)
                throw new Exception("Primary exchange rate currency cannot be loaded");

            var result = amount;
            if (result == decimal.Zero || sourceCurrencyCode.Id == primaryExchangeRateCurrency.Id)
                return result;

            var exchangeRate = sourceCurrencyCode.Rate;
            if (exchangeRate == decimal.Zero)
                throw new NopException($"Exchange rate not found for currency [{sourceCurrencyCode.Name}]");
            result = result / exchangeRate;

            return result;
        }

        /// <summary>
        /// Converts from primary exchange rate currency
        /// </summary>
        /// <param name="amount">Amount</param>
        /// <param name="targetCurrencyCode">Target currency code</param>
        /// <returns>
        /// A task that represents the asynchronous operation
        /// The task result contains the converted value
        /// </returns>
        public virtual async Task<decimal> ConvertFromPrimaryExchangeRateCurrencyAsync(decimal amount, Currency targetCurrencyCode)
        {
            if (targetCurrencyCode == null)
                throw new ArgumentNullException(nameof(targetCurrencyCode));

            var primaryExchangeRateCurrency = await GetCurrencyByIdAsync(CurrencySettings.PrimaryExchangeRateCurrencyId);
            if (primaryExchangeRateCurrency == null)
                throw new Exception("Primary exchange rate currency cannot be loaded");

            var result = amount;
            if (result == decimal.Zero || targetCurrencyCode.Id == primaryExchangeRateCurrency.Id)
                return result;

            var exchangeRate = targetCurrencyCode.Rate;
            if (exchangeRate == decimal.Zero)
                throw new NopException($"Exchange rate not found for currency [{targetCurrencyCode.Name}]");
            result = result * exchangeRate;

            return result;
        }

        #endregion

        #endregion
    }
}