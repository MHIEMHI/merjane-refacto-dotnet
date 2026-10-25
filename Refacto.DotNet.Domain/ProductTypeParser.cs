namespace Refacto.DotNet.Domain.Products
{
    public static class ProductTypeParser
    {
        public static bool TryParse(string? value, out ProductType productType)
        {
            switch (value)
            {
                case "NORMAL":
                    productType = ProductType.Normal;
                    return true;
                case "SEASONAL":
                    productType = ProductType.Seasonal;
                    return true;
                case "EXPIRABLE":
                    productType = ProductType.Expirable;
                    return true;
                default:
                    productType = default;
                    return false;
            }
        }
    }
}
