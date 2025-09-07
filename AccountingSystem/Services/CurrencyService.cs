using AccountingSystem.Models;

namespace AccountingSystem.Services
{
    public class CurrencyService : ICurrencyService
    {
        public decimal Convert(decimal amount, Currency fromCurrency, Currency toCurrency)
        {
            if (fromCurrency == null || toCurrency == null)
                throw new ArgumentNullException();
            if (fromCurrency.Id == toCurrency.Id)
                return amount;
            if (toCurrency.ExchangeRate == 0)
                return 0;
            return amount * fromCurrency.ExchangeRate / toCurrency.ExchangeRate;
        }
    }
}
