const FOOD_ICON_RULES: Array<{ patterns: string[]; icon: string }> = [
  {
    patterns: ["بيتزا", "pizza", "مارجرينا", "رومي", "سلامي", "pepperoni"],
    icon: "🍕",
  },
  {
    patterns: ["بطاطس", "fries", "potato"],
    icon: "🍟",
  },
  {
    patterns: ["تومية", "ثومية", "garlic"],
    icon: "🧄",
  },
  {
    patterns: ["صوص", "كاتشب", "مايونيز", "رانش", "باربكيو", "sauce"],
    icon: "🥣",
  },
  {
    patterns: ["شيكولاتة", "شوكولاتة", "كراميل", "chocolate", "caramel"],
    icon: "🍫",
  },
  {
    patterns: [
      "فراخ",
      "دجاج",
      "شيش",
      "استربس",
      "كرسبي",
      "زنجر",
      "chicken",
      "crispy",
      "strips",
    ],
    icon: "🍗",
  },
  {
    patterns: ["سجق", "لحوم", "مدخن", "meat", "sausage", "smoked"],
    icon: "🥩",
  },
  {
    patterns: ["كريب", "crepe"],
    icon: "🌯",
  },
  {
    patterns: ["مقبلات", "appetizers"],
    icon: "🍽️",
  },
];

const CATEGORY_ICON_RULES: Array<{ patterns: string[]; icon: string }> = [
  { patterns: ["بيتزا", "pizza"], icon: "🍕" },
  { patterns: ["كريب", "crepe"], icon: "🌯" },
  { patterns: ["مقبلات", "appetizers"], icon: "🍟" },
];

const normalizeText = (value?: string | null): string =>
  (value ?? "").trim().toLocaleLowerCase();

const isImageSource = (value: string): boolean =>
  /^(https?:\/\/|\/|data:image\/|blob:)/i.test(value);

const resolveIconFromRules = (
  value: string,
  rules: Array<{ patterns: string[]; icon: string }>,
  fallback: string,
): string => {
  for (const rule of rules) {
    if (rule.patterns.some((pattern) => value.includes(pattern))) {
      return rule.icon;
    }
  }

  return fallback;
};

export const resolveProductIcon = (
  productName: string,
  categoryName?: string | null,
): string => {
  const text = `${normalizeText(productName)} ${normalizeText(categoryName)}`;
  return resolveIconFromRules(text, FOOD_ICON_RULES, "🍽️");
};

export const resolveCategoryIcon = (
  categoryName: string,
  explicitIcon?: string | null,
): string => {
  const normalizedIcon = explicitIcon?.trim();
  if (normalizedIcon && !isImageSource(normalizedIcon)) {
    return normalizedIcon;
  }

  return resolveIconFromRules(
    normalizeText(categoryName),
    CATEGORY_ICON_RULES,
    "🍽️",
  );
};
