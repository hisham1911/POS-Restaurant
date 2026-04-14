namespace KasserPro.Infrastructure.Data;

using KasserPro.Domain.Entities;
using Microsoft.EntityFrameworkCore;

/// <summary>
/// Ensures seeded catalog entities always have meaningful icons.
/// Applies only to known demo-seeded tenants to avoid touching customer tenants.
/// </summary>
public static class SeedCatalogIconSynchronizer
{
    private static readonly Dictionary<string, string> ButcherProductIconMap = new(StringComparer.Ordinal)
    {
        ["قراقيش"] = "🥩",
        ["لحم قطع"] = "🥩",
        ["لحم ضلعه"] = "🍖",
        ["لحم مميز"] = "🥩",
        ["لحم دوش"] = "🥩",
        ["لحم موزه"] = "🥩",
        ["لحم راس"] = "🥩",
        ["مكعبات لحم احمر"] = "🥩",
        ["استيك"] = "🥩",
        ["بوفتيك"] = "🥩",
        ["كباب حله"] = "🍢",
        ["كباب حله احمر"] = "🍢",
        ["برجر"] = "🍔",
        ["سجق مخصوص"] = "🍖",
        ["مفروم مخصوص"] = "🥩",
        ["مزاليكا"] = "🥩",
        ["كوارع بالكيلو"] = "🥩",
        ["كوارع"] = "🥩",
        ["دهن طرب"] = "🥩",
        ["حلويات"] = "🥩",
        ["طحال"] = "🥩",
        ["ممبار"] = "🥩",
        ["ام الشلاتيت"] = "🥩",
        ["دهن كلاوي"] = "🥩",
        ["qaraqish"] = "🥩",
        ["meat cuts"] = "🥩",
        ["ribs"] = "🍖",
        ["premium meat"] = "🥩",
        ["doush meat"] = "🥩",
        ["mouza meat"] = "🥩",
        ["head meat"] = "🥩",
        ["red meat cubes"] = "🥩",
        ["steak"] = "🥩",
        ["beefsteak"] = "🥩",
        ["kebab halla"] = "🍢",
        ["red kebab halla"] = "🍢",
        ["burger"] = "🍔",
        ["special sausage"] = "🍖",
        ["special minced"] = "🥩",
        ["mazalika"] = "🥩",
        ["trotters per kg"] = "🥩",
        ["trotters"] = "🥩",
        ["fat tarab"] = "🥩",
        ["sweetbreads"] = "🥩",
        ["spleen"] = "🥩",
        ["mumbar"] = "🥩",
        ["um el-shalatit"] = "🥩",
        ["kidney fat"] = "🥩"
    };

    public static async Task SynchronizeAsync(AppDbContext context)
    {
        Console.WriteLine("🔄 مزامنة أيقونات الأصناف والمنتجات...");

        var seedTenants = await context.Tenants
            .AsNoTracking()
            .Where(t => SeedTenantRegistry.Slugs.Contains(t.Slug))
            .Select(t => t.Id)
            .ToListAsync();

        if (seedTenants.Count == 0)
        {
            Console.WriteLine("   ✓ لم يتم العثور على مستأجرين تجريبيين للمزامنة");
            return;
        }

        var categories = await context.Categories
            .Where(c => seedTenants.Contains(c.TenantId))
            .ToListAsync();

        var categoryIconById = new Dictionary<int, string>(categories.Count);
        var updatedCategories = 0;

        foreach (var category in categories)
        {
            var resolvedIcon = ResolveCategoryIcon(category.Name, category.NameEn);
            var currentIcon = NormalizeIcon(category.ImageUrl);

            // Always enforce deterministic icon mapping for seed tenants.
            if (!string.Equals(currentIcon, resolvedIcon, StringComparison.Ordinal))
            {
                category.ImageUrl = resolvedIcon;
                currentIcon = resolvedIcon;
                updatedCategories++;
            }

            categoryIconById[category.Id] = resolvedIcon;
        }

        var products = await context.Products
            .Where(p => seedTenants.Contains(p.TenantId))
            .ToListAsync();

        var updatedProducts = 0;

        foreach (var product in products)
        {
            var categoryIcon = categoryIconById.TryGetValue(product.CategoryId, out var icon)
                ? icon
                : "📦";

            var resolvedIcon = ResolveProductIcon(product.Name, product.NameEn, categoryIcon);
            var currentIcon = NormalizeIcon(product.ImageUrl);

            if (!string.Equals(currentIcon, resolvedIcon, StringComparison.Ordinal))
            {
                product.ImageUrl = resolvedIcon;
                updatedProducts++;
            }
        }

        if (updatedCategories > 0 || updatedProducts > 0)
        {
            await context.SaveChangesAsync();
        }

        Console.WriteLine($"   ✓ مزامنة الأيقونات: أصناف {updatedCategories} | منتجات {updatedProducts}");
    }

    private static string NormalizeIcon(string? icon)
    {
        return string.IsNullOrWhiteSpace(icon) ? string.Empty : icon.Trim();
    }

    private static string ResolveCategoryIcon(string? name, string? nameEn)
    {
        var text = NormalizeText(name, nameEn);

        if (ContainsAny(text, "أدوات مطبخ", "kitchen")) return "🔪";
        if (ContainsAny(text, "أجهزة كهربائية", "electrical", "appliance")) return "🔌";
        if (ContainsAny(text, "أواني", "أطباق", "cookware", "dish")) return "🍽️";
        if (ContainsAny(text, "مشروبات ساخنة", "hot drink", "coffee", "tea")) return "☕";
        if (ContainsAny(text, "مشروبات", "beverage", "drinks", "drink")) return "🥤";
        if (ContainsAny(text, "بقالة", "grocery")) return "🛒";
        if (ContainsAny(text, "ألبان", "dairy", "milk")) return "🥛";
        if (ContainsAny(text, "خبز", "مخبوزات", "bakery")) return "🥖";
        if (ContainsAny(text, "لحوم ودواجن", "meat & poultry")) return "🥩";
        if (ContainsAny(text, "دواجن", "poultry", "chicken")) return "🍗";
        if (ContainsAny(text, "مفرومة", "مصنعة", "minced", "processed", "sausage")) return "🍖";
        if (ContainsAny(text, "أحشاء", "offal")) return "🥩";
        if (ContainsAny(text, "مجمدة", "frozen")) return "❄️";
        if (ContainsAny(text, "لحوم", "meat", "beef", "poultry", "grill", "مشويات")) return "🥩";
        if (ContainsAny(text, "خضروات", "فواكه", "vegetable", "fruit", "produce")) return "🥬";
        if (ContainsAny(text, "حلويات", "dessert", "sweets", "chocolate")) return "🍰";
        if (ContainsAny(text, "مقبلات", "appetizer")) return "🥗";
        if (ContainsAny(text, "مأكولات", "food")) return "🍔";
        if (ContainsAny(text, "وجبات خفيفة", "snacks")) return "🍿";
        if (ContainsAny(text, "منظفات", "cleaning", "detergent")) return "🧴";
        if (ContainsAny(text, "عناية شخصية", "personal care", "toiletries")) return "🧼";
        if (ContainsAny(text, "أدوات منزلية", "household")) return "🏠";

        return "🏷️";
    }

    private static string ResolveProductIcon(string? name, string? nameEn, string categoryIcon)
    {
        var normalizedName = NormalizeSingle(name);
        if (ButcherProductIconMap.TryGetValue(normalizedName, out var explicitButcherIcon))
        {
            return explicitButcherIcon;
        }

        var normalizedNameEn = NormalizeSingle(nameEn);
        if (ButcherProductIconMap.TryGetValue(normalizedNameEn, out explicitButcherIcon))
        {
            return explicitButcherIcon;
        }

        var text = NormalizeText(name, nameEn);

        // Beverages
        if (ContainsAny(text, "شاي", "tea")) return "🍵";
        if (ContainsAny(text, "قهوة", "coffee", "espresso", "cappuccino", "latte", "mocha", "نسكافيه")) return "☕";
        if (ContainsAny(text, "hot chocolate", "شوكولاتة ساخنة")) return "🍫";
        if (ContainsAny(text, "مياه", "water")) return "💧";
        if (ContainsAny(text, "orange", "برتقال")) return "🍊";
        if (ContainsAny(text, "mango", "مانجو")) return "🥭";
        if (ContainsAny(text, "strawberry", "فراولة")) return "🍓";
        if (ContainsAny(text, "banana", "موز")) return "🍌";
        if (ContainsAny(text, "apple", "تفاح")) return "🍎";
        if (ContainsAny(text, "lemon", "ليمون")) return "🍋";
        if (ContainsAny(text, "cola", "soda", "غازي", "soft drink", "energy drink")) return "🥤";
        if (ContainsAny(text, "juice", "عصير", "smoothie", "ليموناضة", "iced tea")) return "🧃";

        // Grocery
        if (ContainsAny(text, "rice", "أرز")) return "🍚";
        if (ContainsAny(text, "sugar", "سكر")) return "🍬";
        if (ContainsAny(text, "oil", "زيت")) return "🫒";
        if (ContainsAny(text, "pasta", "معكرونة")) return "🍝";
        if (ContainsAny(text, "flour", "طحين")) return "🌾";
        if (ContainsAny(text, "salt", "ملح")) return "🧂";
        if (ContainsAny(text, "lentils", "beans", "chickpeas", "عدس", "فول", "حمص", "فاصوليا")) return "🫘";
        if (ContainsAny(text, "honey", "عسل")) return "🍯";
        if (ContainsAny(text, "jam", "مربى")) return "🍓";

        // Dairy
        if (ContainsAny(text, "milk", "حليب", "لبن", "yogurt", "زبادي")) return "🥛";
        if (ContainsAny(text, "cheese", "جبنة")) return "🧀";
        if (ContainsAny(text, "butter", "زبدة")) return "🧈";
        if (ContainsAny(text, "cream", "قشطة")) return "🥣";

        // Bakery / Food
        if (ContainsAny(text, "bread", "خبز", "toast")) return "🍞";
        if (ContainsAny(text, "croissant", "كرواسون")) return "🥐";
        if (ContainsAny(text, "pizza", "بيتزا")) return "🍕";
        if (ContainsAny(text, "sandwich", "ساندويتش")) return "🥪";
        if (ContainsAny(text, "burger", "برجر")) return "🍔";
        if (ContainsAny(text, "salad", "سلطة")) return "🥗";
        if (ContainsAny(text, "pie", "فطيرة")) return "🥧";
        if (ContainsAny(text, "kebab", "kofta", "كباب", "كفتة", "grill", "مشويات")) return "🍢";

        // Meat / Butcher
        if (ContainsAny(text, "offal", "كوارع", "طحال", "ممبار", "دهن", "شلاتيت", "trotters", "spleen", "kidney")) return "🥩";
        if (ContainsAny(text, "poultry", "chicken", "دواجن")) return "🍗";
        if (ContainsAny(text, "meat", "beef", "lamb", "chicken", "steak", "rib", "sausage", "مفروم", "لحم", "استيك", "سجق", "دواجن")) return "🥩";

        // Sweets / Snacks
        if (ContainsAny(text, "cake", "cheesecake", "brownies", "كيك", "براونيز")) return "🍰";
        if (ContainsAny(text, "ice cream", "آيس كريم")) return "🍨";
        if (ContainsAny(text, "donut", "دونات")) return "🍩";
        if (ContainsAny(text, "cookie", "biscuit", "wafer", "كوكيز", "بسكويت", "ويفر")) return "🍪";
        if (ContainsAny(text, "chocolate", "شوكولاتة")) return "🍫";
        if (ContainsAny(text, "chips", "شيبس")) return "🥔";
        if (ContainsAny(text, "popcorn", "فشار")) return "🍿";
        if (ContainsAny(text, "nuts", "مكسرات")) return "🥜";
        if (ContainsAny(text, "candy", "gum", "حلوى", "علكة")) return "🍬";

        // Produce
        if (ContainsAny(text, "tomato", "طماطم")) return "🍅";
        if (ContainsAny(text, "cucumber", "خيار")) return "🥒";
        if (ContainsAny(text, "potato", "بطاطس")) return "🥔";
        if (ContainsAny(text, "onion", "بصل")) return "🧅";

        // Cleaning / Personal care
        if (ContainsAny(text, "sponge", "إسفنجة")) return "🧽";
        if (ContainsAny(text, "toothbrush", "toothpaste", "فرشاة", "معجون")) return "🪥";
        if (ContainsAny(text, "soap", "shampoo", "cleaner", "detergent", "bleach", "deodorant", "moisturizer", "صابون", "شامبو", "منظف", "كلور", "مزيل", "مرطب")) return "🧴";
        if (ContainsAny(text, "tissue", "paper", "مناديل", "ورق")) return "🧻";
        if (ContainsAny(text, "garbage", "أكياس قمامة")) return "🗑️";

        // Home appliances
        if (ContainsAny(text, "knife", "سكاكين")) return "🔪";
        if (ContainsAny(text, "peeler", "مقشرة")) return "🥕";
        if (ContainsAny(text, "blender", "toaster", "electric", "خلاط", "محمصة")) return "🔌";
        if (ContainsAny(text, "dish", "plate", "cup", "أطباق", "أكواب")) return "🍽️";

        return string.IsNullOrWhiteSpace(categoryIcon) ? "📦" : categoryIcon;
    }

    private static string NormalizeText(params string?[] parts)
    {
        var tokens = parts
            .Where(p => !string.IsNullOrWhiteSpace(p))
            .Select(p => p!.Trim().ToLowerInvariant());

        return string.Join(' ', tokens);
    }

    private static string NormalizeSingle(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim().ToLowerInvariant();
    }

    private static bool ContainsAny(string source, params string[] keywords)
    {
        foreach (var keyword in keywords)
        {
            if (!string.IsNullOrWhiteSpace(keyword) && source.Contains(keyword.ToLowerInvariant(), StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }
}
