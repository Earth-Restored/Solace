using System.Globalization;
using System.Text;
using System.Text.Json.Serialization;
using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Solace.ApiServer.Utils;
using Solace.Common.Utils;
using Solace.StaticData;

namespace Solace.ApiServer.Controllers;

[Authorize]
[ApiVersion("1.1")]
[Route("1/api/v{version:apiVersion}/products")]
internal sealed class ProductsController : SolaceControllerBase
{
    private readonly Catalog _catalog;

    public ProductsController(StaticDataProvider staticData)
    {
        _catalog = staticData.Catalog;
    }

    [HttpPost("getProductInfo")]
    public async Task<Results<ContentHttpResult, BadRequest<string>>> GetProductInfo(CancellationToken cancellationToken)
    {
        var request = await Request.Body.AsJsonAsync(AppJsonContext.Default.GetProductInfoRequest, cancellationToken);
        if (request is null)
        {
            return TypedResults.BadRequest("Invalid request data");
        }

        var nfcData = request.NfcChip.Data;

        if (nfcData.Length is 0 || nfcData[0][0] > 2 /* URL Record 2 == https */)
        {
            return TypedResults.BadRequest("Scanned Boost Mini did not provide a valid record to identify with");
        }

        var urlInfo = Encoding.UTF8.GetString(nfcData[0].AsSpan(1));

        if (!urlInfo.StartsWith("pid.mattel/", StringComparison.Ordinal))
        {
            return TypedResults.BadRequest("Scanned Boost Minis URL record does not start with pid.mattel");
        }

        var boostIdData = urlInfo[11..];
        boostIdData += string.Join(string.Empty, Enumerable.Repeat("=", boostIdData.Length % 4));

        var boostIdBytes = new Span<byte>(new byte[boostIdData.Length * 3 / 4]);

        if (Convert.TryFromBase64String(boostIdData, boostIdBytes, out var boostIdByteCount)
            && boostIdByteCount == 24
            && boostIdBytes[0] == 2
            && boostIdBytes[1] == 0)
        {
            var boostIdIntData = boostIdBytes[2..6].ToArray();
            Array.Reverse(boostIdIntData);
            var boostId = BitConverter.ToUInt32(boostIdIntData).ToString();

            var boostUniqueIdData = boostIdBytes[12..20].ToArray();
            Array.Reverse(boostUniqueIdData);
            var uniqueId = BitConverter.ToInt64(boostUniqueIdData).ToString(CultureInfo.InvariantCulture);

            if (_catalog.NfcBoostsCatalog.MiniFigs.TryGetValue(boostId, out var product))
            {
                var productItem = new ProductInfo(product.Id, uniqueId, ProductType.NfcMiniFig);

                return EarthJson(productItem);
            }

            return TypedResults.BadRequest("Scanned Boost Mini has invalid identifier");
        }

        return TypedResults.BadRequest("Failed to parse boostId from scanned Boost Mini");
    }

    internal sealed record GetProductInfoRequest(
        GetProductInfoRequest.NfcChipR NfcChip
    )
    {
        public sealed record NfcChipR(
            [property: JsonConverter(typeof(NestedByteArrayConverter))]
            byte[][] Data
        );
    }

    public sealed record ProductInfo(string Id, string UniqueId, ProductType Type);

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum ProductType
    {
        MiniFig,
        NfcMiniFig,
    }
}
