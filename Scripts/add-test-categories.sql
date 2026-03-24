-- Script to add 30 test categories for pagination testing
-- Run this in your SQLite database

-- Get the TenantId (assuming it's 1 for the default tenant)
-- Categories will be added with TenantId = 1

INSERT INTO Categories (TenantId, Name, NameEn, Description, SortOrder, IsActive, IsDeleted, CreatedAt, UpdatedAt) VALUES
(1, 'مشروبات ساخنة', 'Hot Beverages', 'قهوة وشاي ومشروبات ساخنة', 1, 1, 0, datetime('now'), NULL),
(1, 'مشروبات باردة', 'Cold Beverages', 'عصائر ومشروبات غازية', 2, 1, 0, datetime('now'), NULL),
(1, 'وجبات سريعة', 'Fast Food', 'برجر وساندويتشات', 3, 1, 0, datetime('now'), NULL),
(1, 'معجنات', 'Pastries', 'كرواسون وفطائر', 4, 1, 0, datetime('now'), NULL),
(1, 'حلويات', 'Desserts', 'كيك وآيس كريم', 5, 1, 0, datetime('now'), NULL),
(1, 'سلطات', 'Salads', 'سلطات طازجة', 6, 1, 0, datetime('now'), NULL),
(1, 'مقبلات', 'Appetizers', 'مقبلات متنوعة', 7, 1, 0, datetime('now'), NULL),
(1, 'أطباق رئيسية', 'Main Dishes', 'وجبات رئيسية', 8, 1, 0, datetime('now'), NULL),
(1, 'بيتزا', 'Pizza', 'بيتزا بأنواعها', 9, 1, 0, datetime('now'), NULL),
(1, 'باستا', 'Pasta', 'معكرونة إيطالية', 10, 1, 0, datetime('now'), NULL),
(1, 'مأكولات بحرية', 'Seafood', 'أسماك وجمبري', 11, 1, 0, datetime('now'), NULL),
(1, 'دجاج', 'Chicken', 'أطباق دجاج', 12, 1, 0, datetime('now'), NULL),
(1, 'لحوم', 'Meat', 'لحوم حمراء', 13, 1, 0, datetime('now'), NULL),
(1, 'نباتي', 'Vegetarian', 'أطباق نباتية', 14, 1, 0, datetime('now'), NULL),
(1, 'إفطار', 'Breakfast', 'وجبات إفطار', 15, 1, 0, datetime('now'), NULL),
(1, 'سناكس', 'Snacks', 'وجبات خفيفة', 16, 1, 0, datetime('now'), NULL),
(1, 'شوربة', 'Soup', 'أنواع الشوربة', 17, 1, 0, datetime('now'), NULL),
(1, 'عصائر طبيعية', 'Fresh Juices', 'عصائر طازجة', 18, 1, 0, datetime('now'), NULL),
(1, 'مخبوزات', 'Bakery', 'خبز ومخبوزات', 19, 1, 0, datetime('now'), NULL),
(1, 'آيس كريم', 'Ice Cream', 'آيس كريم بالنكهات', 20, 1, 0, datetime('now'), NULL),
(1, 'مشروبات صحية', 'Healthy Drinks', 'سموذي وديتوكس', 21, 1, 0, datetime('now'), NULL),
(1, 'وجبات أطفال', 'Kids Meals', 'وجبات للأطفال', 22, 1, 0, datetime('now'), NULL),
(1, 'كومبو', 'Combo Meals', 'وجبات كومبو', 23, 1, 0, datetime('now'), NULL),
(1, 'مقليات', 'Fried Food', 'أطعمة مقلية', 24, 1, 0, datetime('now'), NULL),
(1, 'مشويات', 'Grilled', 'مشويات متنوعة', 25, 1, 0, datetime('now'), NULL),
(1, 'أرز', 'Rice', 'أطباق أرز', 26, 1, 0, datetime('now'), NULL),
(1, 'صوصات', 'Sauces', 'صوصات وإضافات', 27, 1, 0, datetime('now'), NULL),
(1, 'مكسرات', 'Nuts', 'مكسرات ومقرمشات', 28, 1, 0, datetime('now'), NULL),
(1, 'فواكه', 'Fruits', 'فواكه طازجة', 29, 1, 0, datetime('now'), NULL),
(1, 'منتجات ألبان', 'Dairy Products', 'حليب وأجبان', 30, 1, 0, datetime('now'), NULL);
