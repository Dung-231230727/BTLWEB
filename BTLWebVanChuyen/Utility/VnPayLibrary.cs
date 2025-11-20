using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Globalization;

namespace BTLWebVanChuyen.Utility
{
    // Class tiện ích giúp tạo URL và xác thực chữ ký VNPay
    public class VnPayLibrary
    {
        private readonly SortedList<string, string> _requestData = new SortedList<string, string>(new VnPayCompare());
        private readonly SortedList<string, string> _responseData = new SortedList<string, string>(new VnPayCompare());

        public void AddRequestData(string key, string value)
        {
            if (!string.IsNullOrEmpty(value)) _requestData.Add(key, value);
        }

        public void AddResponseData(string key, string value)
        {
            if (!string.IsNullOrEmpty(value)) _responseData.Add(key, value);
        }

        public string GetResponseData(string key)
        {
            return _responseData.TryGetValue(key, out var retValue) ? retValue : string.Empty;
        }

        public string CreateRequestUrl(string baseUrl, string vnp_HashSecret)
        {
            StringBuilder data = new StringBuilder();
            foreach (KeyValuePair<string, string> kv in _requestData)
            {
                if (!string.IsNullOrEmpty(kv.Value))
                {
                    if (data.Length > 0) data.Append("&");
                    data.Append(kv.Key + "=" + WebUtility.UrlEncode(kv.Value));
                }
            }
            string queryString = data.ToString();
            string vnp_SecureHash = HmacSHA512(vnp_HashSecret, queryString);
            return baseUrl + "?" + queryString + "&vnp_SecureHash=" + vnp_SecureHash;
        }

        public bool ValidateSignature(string inputHash, string vnp_HashSecret)
        {
            string rspRaw = GetResponseData();
            string myChecksum = HmacSHA512(vnp_HashSecret, rspRaw);
            return myChecksum.Equals(inputHash, StringComparison.InvariantCultureIgnoreCase);
        }

        private string GetResponseData()
        {
            StringBuilder data = new StringBuilder();
            // Loại bỏ các trường không cần thiết trước khi tạo chuỗi Raw
            if (_responseData.ContainsKey("vnp_SecureHashType")) _responseData.Remove("vnp_SecureHashType");
            if (_responseData.ContainsKey("vnp_SecureHash")) _responseData.Remove("vnp_SecureHash");

            foreach (KeyValuePair<string, string> kv in _responseData)
            {
                if (!string.IsNullOrEmpty(kv.Value))
                {
                    if (data.Length > 0) data.Append("&");
                    data.Append(kv.Key + "=" + WebUtility.UrlEncode(kv.Value));
                }
            }
            return data.ToString();
        }

        private string HmacSHA512(string key, string inputData)
        {
            var hash = new StringBuilder();
            byte[] keyBytes = Encoding.UTF8.GetBytes(key);
            byte[] inputBytes = Encoding.UTF8.GetBytes(inputData);
            using (var hmac = new HMACSHA512(keyBytes))
            {
                byte[] hashValue = hmac.ComputeHash(inputBytes);
                foreach (var theByte in hashValue)
                {
                    hash.Append(theByte.ToString("x2"));
                }
            }
            return hash.ToString();
        }
    }

    // Class so sánh đặc biệt cho VNPay (sắp xếp theo thứ tự từ điển)
    public class VnPayCompare : IComparer<string>
    {
        public int Compare(string? x, string? y)
        {
            if (x == y) return 0;
            if (x == null) return -1;
            if (y == null) return 1;
            var vnpCompare = CompareInfo.GetCompareInfo("en-US");
            return vnpCompare.Compare(x, y, CompareOptions.Ordinal);
        }
    }
}