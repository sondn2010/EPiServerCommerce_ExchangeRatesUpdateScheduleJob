[ScheduledPlugIn(
    DisplayName = "Exchange Rates update job",
    Description = "This job update exchange rates.")]
public class ExchangeRatesUpdateJob : ScheduledJobBase
{
    [NonSerialized]
    private static readonly ILogger _log = LogManager.GetLogger(typeof(ExchangeRatesUpdateJob));
        
    public override string Execute()
    {
        string message = string.Empty;
        int records = UpdateExchangeRate(out message);
        return $"{records.ToString()} currencies were updated. {message}. Exchange Rate update Job was finished.";
    }
        
    private static int UpdateExchangeRate(out string message)
    {
        message = string.Empty;
        _log.Information("Starting to update exchange rate job.");
        var processedCurrency = 0;
        var dto = CurrencyManager.GetCurrencyDto();
            
        var currencyCodeIdMapping = dto.Currency.ToDictionary(c => c.CurrencyCode, c => c.CurrencyId);

        try
        {
            foreach (var code in currencyCodeIdMapping.Keys)
            {
                int fromCurrencyId;
                currencyCodeIdMapping.TryGetValue(code, out fromCurrencyId);

                try
                {
                    var uri = $"http://api.fixer.io/latest?base={code}";
                    using (var client = new WebClient())
                    {
                        var json = client.DownloadString(uri);
                        dynamic d = JObject.Parse(json);

                        DateTime exchangeDate = d.date; 

                        foreach (var rate in d.rates)
                        {
                            string toCurrencyCode = rate.Name;
                            int toCurrencyId;
                            if (currencyCodeIdMapping.TryGetValue(toCurrencyCode, out toCurrencyId))
                            {

                                var currencyRate = dto.CurrencyRate.NewCurrencyRateRow();
                                currencyRate.FromCurrencyId = fromCurrencyId;
                                currencyRate.ToCurrencyId = toCurrencyId;

                                currencyRate.EndOfDayRate = currencyRate.AverageRate = rate.Value;
                                currencyRate.CurrencyRateDate = exchangeDate;
                                currencyRate.ModifiedDate = DateTime.Now;
                                dto.CurrencyRate.AddCurrencyRateRow(currencyRate);
                                currencyRate.AcceptChanges();
                            }
                        }

                        dto.CurrencyRate.AcceptChanges();
                    }
                }
                catch (Exception ex)
                {
                    message += "Cannot update currency {code}. ";
                    _log.Error("Cannot update currency {code}.", ex);
                    continue;
                }

                dto.AcceptChanges();
                processedCurrency++;
                _log.Information($"{code} rates were updated");
            }
        }
        catch (Exception ex)
        {
            _log.Error("The job could not be completed. ", ex);
        }
        return processedCurrency;
    }
}
