using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Server.Infrastructure.Configuration;

namespace Server.Client.Payments
{
    public class NowPaymentsService
    {
        private readonly HttpClient _httpClient;
        private readonly string _apiKey;
        private readonly string _baseUrl = "https://api.nowpayments.io/v1";

        public NowPaymentsService(string apiKey)
        {
            _apiKey = apiKey;
            _httpClient = new HttpClient();
            _httpClient.DefaultRequestHeaders.Add("x-api-key", _apiKey);
        }

        public async Task<PaymentResponse> CreatePaymentAsync(double priceAmount, string priceCurrency, string payCurrency, string orderId, string orderDescription)
        {
            var request = new
            {
                price_amount = priceAmount,
                price_currency = priceCurrency,
                pay_currency = payCurrency,
                // ipn_callback_url = "https://your-callback-url.com/webhook", // Removed to use Dashboard setting
                order_id = orderId,
                order_description = orderDescription
            };

            var json = JsonSerializer.Serialize(request);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            try
            {
                var response = await _httpClient.PostAsync($"{_baseUrl}/payment", content);
                response.EnsureSuccessStatusCode();
                var responseString = await response.Content.ReadAsStringAsync();
                return JsonSerializer.Deserialize<PaymentResponse>(responseString);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"NowPayments Error: {ex.Message}");
                return null;
            }
        }
        
        public async Task<InvoiceResponse> CreateInvoiceAsync(double priceAmount, string priceCurrency, string orderId, string orderDescription)
        {
            var callbackUrl = ConfigService.Current.Payments?.IpnCallbackUrl;

            // Use a dictionary to conditionally add properties
            var requestData = new Dictionary<string, object>
            {
                { "price_amount", priceAmount },
                { "price_currency", priceCurrency },
                { "order_id", orderId },
                { "order_description", orderDescription }
                // Removed success_url and cancel_url pointing to @me to avoid Unauthorized errors
            };

            if (!string.IsNullOrEmpty(callbackUrl))
            {
                requestData.Add("ipn_callback_url", callbackUrl);
            }

            var json = JsonSerializer.Serialize(requestData);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            try
            {
                var response = await _httpClient.PostAsync($"{_baseUrl}/invoice", content);
                response.EnsureSuccessStatusCode();
                var responseString = await response.Content.ReadAsStringAsync();
                return JsonSerializer.Deserialize<InvoiceResponse>(responseString);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"NowPayments Invoice Error: {ex.Message}");
                return null;
            }
        }
    }

    public class PaymentResponse
    {
        public string payment_id { get; set; }
        public string payment_status { get; set; }
        public string pay_address { get; set; }
        public double price_amount { get; set; }
        public string price_currency { get; set; }
        public double pay_amount { get; set; }
        public string pay_currency { get; set; }
        public string order_id { get; set; }
        public string order_description { get; set; }
        public string ipn_callback_url { get; set; }
        public string created_at { get; set; }
        public string updated_at { get; set; }
        public string purchase_id { get; set; }
    }
    
    public class InvoiceResponse
    {
        public string id { get; set; }
        public string order_id { get; set; }
        public string order_description { get; set; }
        public string price_amount { get; set; }
        public string price_currency { get; set; }
        public string pay_currency { get; set; }
        public string ipn_callback_url { get; set; }
        public string invoice_url { get; set; }
        public string success_url { get; set; }
        public string cancel_url { get; set; }
        public string created_at { get; set; }
        public string updated_at { get; set; }
    }
}
