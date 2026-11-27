using System.Globalization;
using UnityEngine;
using UnityEngine.UI;

namespace Storybook
{
    [DefaultExecutionOrder(150), DisallowMultipleComponent]
    public sealed class ShopPresentation : MonoBehaviour
    {
        static readonly Color Ink = new(.98f, .93f, .82f);
        static readonly Color Gold = new(.64f, .52f, .32f);
        static readonly Color Muted = new(.81f, .75f, .66f);
        static readonly Color Shortage = new(1f, .48f, .45f);
        static readonly Color Panel = new(.07f, .058f, .10f, .95f);
        const float Margin = 10f, LabelHeight = 24f, DetailChrome = 119f;

        readonly PriceLabel[] prices = new PriceLabel[4];
        ShopPedestalRow shop;
        PlayerHealth player;
        HongGildongHud playerHud;
        PalaceDungeonMinimap minimap;
        Camera view;
        Canvas canvas;
        RectTransform surface, detail;
        Text title, classification, description, detailPrice, purchaseHint, purchaseStatus;
        Texture2D fragmentIcon;
        Font font;
        bool ownsFont;
        Material highlightMaterial;
        HongGildong hero;
        int interactionFrame = -1;

        internal bool HasOfferInRange => interactionFrame == Time.frameCount || FindOffer() != null;
        internal float PurchaseFeedbackDuration => shop != null ? shop.PurchaseFeedbackDuration : 1.6f;
        internal AudioClip PurchaseSound => shop != null ? shop.PurchaseSound : null;

        public ShopPedestal SelectedPedestal { get; private set; }
        public ShopProduct SelectedProduct => SelectedPedestal != null && SelectedPedestal.Display != null
            ? SelectedPedestal.Display.Product : null;

        internal void Initialize(PlayerHealth owner, Camera camera, PalaceDungeonMinimap dungeonHud)
        {
            player = owner;
            hero = owner.GetComponent<HongGildong>();
            view = camera;
            minimap = dungeonHud;
            playerHud = owner.GetComponent<HongGildongHud>();
        }

        internal void SetShop(ShopPedestalRow row)
        {
            if (shop == row) return;
            Select(null);
            shop = row;
            if (canvas != null) canvas.enabled = false;
        }

        ShopPedestal FindOffer() => shop != null && shop.isActiveAndEnabled && player != null && !player.IsDead
            ? shop.FindNearestOffer(player.transform.position) : null;

        bool BlocksPresentation => shop == null || !shop.isActiveAndEnabled || player == null || player.IsDead ||
            view == null || Time.timeScale <= 0f || StorybookTransition.IsBusy || RelicInventoryPanel.BlocksGameplay ||
            NpcDialogue.BlocksInteraction
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            || PalaceDeveloperMenu.BlocksGameplay
#endif
            ;

        internal bool TryInteract(HongGildong buyer, out string message, out ShopProduct purchased)
        {
            message = null;
            purchased = null;
            if (interactionFrame == Time.frameCount) return true;
            if (BlocksPresentation) return false;
            var target = FindOffer();
            if (target == null) return false;

            interactionFrame = Time.frameCount;
            // Never buy a different stand while the screen still shows the previous target.
            if (target != SelectedPedestal || detail == null || !detail.gameObject.activeInHierarchy) return true;
            if (buyer != hero)
            {
                message = "지금은 구매할 수 없습니다.";
                return true;
            }

            var product = target.Display.Product;
            if (ShopPurchase.TryBuy(target, player, buyer, out message))
            {
                purchased = product;
                Select(null);
            }
            return true;
        }

        void LateUpdate()
        {
            if (BlocksPresentation)
            {
                Select(null);
                if (canvas != null) canvas.enabled = false;
                return;
            }

            if (canvas == null) Build();
            canvas.enabled = true;
            float width = surface.rect.width;
            float height = surface.rect.height;
            ShopPedestal nearest = FindOffer();
            Rect goods = default;
            int visibleProducts = 0;
            for (int slot = 0; slot < prices.Length; slot++)
            {
                var pedestal = shop.Pedestals[slot];
                var display = pedestal != null ? pedestal.Display : null;
                Vector2 anchor = default;
                bool sold = pedestal != null && pedestal.IsSold;
                bool visible = (sold || (display != null && display.isActiveAndEnabled)) &&
                    Project(pedestal.transform.TransformPoint(shop.PriceOffset), out anchor);
                var price = prices[slot];
                price.Root.gameObject.SetActive(visible);
                if (!visible) continue;
                price.Root.anchoredPosition = anchor;
                if (sold)
                {
                    price.SetSold();
                    continue;
                }

                var product = display.Product;
                price.Set(product.Price, player.StoryFragments >= product.Price);
                if (!Project(display.DetailAnchor, out var top)) continue;
                float halfWidth = Mathf.Max(price.Root.rect.width * .5f, 38f);
                Rect offer = Rect.MinMaxRect(anchor.x - halfWidth, anchor.y - LabelHeight, anchor.x + halfWidth, top.y);
                goods = visibleProducts++ == 0 ? offer : Rect.MinMaxRect(Mathf.Min(goods.xMin, offer.xMin),
                    Mathf.Min(goods.yMin, offer.yMin), Mathf.Max(goods.xMax, offer.xMax), Mathf.Max(goods.yMax, offer.yMax));
            }

            Select(nearest);
            if (nearest == null || visibleProducts == 0 || width <= 0 || height <= 0 ||
                !Project(nearest.Display.DetailAnchor, out var target))
            {
                detail.gameObject.SetActive(false);
                return;
            }
            DrawDetail(SelectedProduct, target, goods, width, height);
        }

        bool Project(Vector3 point, out Vector2 position)
        {
            Vector3 screen = view.WorldToScreenPoint(point);
            RectTransformUtility.ScreenPointToLocalPointInRectangle(surface, screen, null, out position);
            position -= surface.rect.min;
            return screen.z > 0f && view.pixelRect.Contains(screen);
        }

        void Select(ShopPedestal pedestal)
        {
            if (SelectedPedestal == pedestal) return;
            if (SelectedPedestal != null && SelectedPedestal.Display != null)
                SelectedPedestal.Display.SetHighlighted(false, highlightMaterial);
            SelectedPedestal = pedestal;
            if (pedestal != null) pedestal.Display.SetHighlighted(true, highlightMaterial);
            else if (detail != null) detail.gameObject.SetActive(false);
        }

        void DrawDetail(ShopProduct product, Vector2 target, Rect goods, float width, float height)
        {
            title.text = product.DisplayName;
            classification.text = product.Kind == ShopProductKind.Relic ? "동화 유물" : $"{GradeName(product.Grade)} · 회복 상품";
            description.text = product.Kind == ShopProductKind.Relic ? product.Description
                : $"최대 {(product.Kind == ShopProductKind.HealthOrb ? "HP" : "MP")}의 {product.RecoveryFraction.ToString("0.##%", CultureInfo.InvariantCulture)}를 회복합니다.";
            bool affordable = player.StoryFragments >= product.Price;
            detailPrice.text = product.Price.ToString("N0");
            detailPrice.color = affordable ? Ink : Shortage;
            string reason = ShopPurchase.BlockReason(SelectedPedestal, player, hero, product.Price);
            purchaseHint.color = reason == null ? Ink : Muted;
            purchaseStatus.text = reason ?? string.Empty;

            float bottom = playerHud != null ? playerHud.SkillRowTop / canvas.scaleFactor + Margin : 96f;
            var safe = Rect.MinMaxRect(Margin, bottom, width - Margin, height - Margin);
            Rect map = ToSurface(minimap != null ? minimap.ScreenBounds : default);
            Rect resources = ToSurface(playerHud != null ? playerHud.ResourceScreenBounds : default);
            float panelWidth = Mathf.Min(shop.DetailWidth, safe.width);
            if (panelWidth < 180f)
            {
                detail.gameObject.SetActive(false);
                return;
            }
            description.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, panelWidth - 28f);
            description.resizeTextForBestFit = false;
            float desiredHeight = DetailChrome + Mathf.Max(20f, Mathf.Ceil(description.preferredHeight));
            Vector2 size = new(panelWidth, desiredHeight);
            bool fits = false;
            Rect placement = default;
            for (float h = Mathf.Min(desiredHeight, safe.height); h >= DetailChrome + 20f; h -= 10f)
            {
                size.y = h;
                if (!TryPlace(safe, goods, target, size, resources, map, out placement)) continue;
                fits = true;
                break;
            }

            detail.gameObject.SetActive(fits);
            if (!fits) return;
            detail.anchoredPosition = placement.min;
            detail.sizeDelta = size;
            description.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, size.y - DetailChrome);
            description.resizeTextForBestFit = size.y < desiredHeight;
        }

        Rect ToSurface(Rect pixels)
        {
            float scale = canvas.scaleFactor;
            return new Rect(pixels.position / scale, pixels.size / scale);
        }

        static bool TryPlace(Rect safe, Rect goods, Vector2 target, Vector2 size, Rect resources, Rect map, out Rect result)
        {
            var above = Rect.MinMaxRect(safe.xMin, Mathf.Max(safe.yMin, goods.yMax + Margin), safe.xMax, safe.yMax);
            var below = Rect.MinMaxRect(safe.xMin, safe.yMin, safe.xMax, Mathf.Min(safe.yMax, goods.yMin - Margin));
            Vector2 desired = new(target.x - size.x * .5f, target.y + Margin);
            return TryFit(above, desired, size, resources, map, out result)
                || TryFit(below, new Vector2(desired.x, goods.yMin - Margin - size.y), size, resources, map, out result)
                || TryFit(Rect.MinMaxRect(safe.xMin, safe.yMin, goods.xMin - Margin, safe.yMax),
                    new Vector2(goods.xMin - Margin - size.x, desired.y), size, resources, map, out result)
                || TryFit(Rect.MinMaxRect(goods.xMax + Margin, safe.yMin, safe.xMax, safe.yMax),
                    new Vector2(goods.xMax + Margin, desired.y), size, resources, map, out result);
        }

        static bool TryFit(Rect area, Vector2 desired, Vector2 size, Rect resources, Rect map, out Rect result)
        {
            result = default;
            if (size.x > area.width || size.y > area.height) return false;
            if (TryPosition(area, desired, size, resources, map, out result)) return true;
            bool hitsResources = result.Overlaps(resources);
            bool hitsMap = result.Overlaps(map);
            float hudBottom = hitsResources ? resources.yMin : map.yMin;
            if (hitsMap) hudBottom = Mathf.Min(hudBottom, map.yMin);
            return TryPosition(area, new Vector2(desired.x, hudBottom - Margin - size.y), size, resources, map, out result)
                || hitsMap && TryPosition(area, new Vector2(map.xMin - Margin - size.x, desired.y),
                    size, resources, map, out result)
                || hitsResources && TryPosition(area, new Vector2(resources.xMax + Margin, desired.y),
                    size, resources, map, out result);
        }

        static bool TryPosition(Rect area, Vector2 desired, Vector2 size, Rect resources, Rect map, out Rect result)
        {
            result = new Rect(Mathf.Clamp(desired.x, area.xMin, area.xMax - size.x),
                Mathf.Clamp(desired.y, area.yMin, area.yMax - size.y), size.x, size.y);
            return !result.Overlaps(resources) && !result.Overlaps(map);
        }

        static string GradeName(ShopProductGrade grade) => grade switch
        {
            ShopProductGrade.Low => "하급",
            ShopProductGrade.Medium => "중급",
            _ => "상급"
        };

        void Build()
        {
            surface = MakeRect("Shop presentation", null, Vector2.zero, Vector2.zero, Vector2.zero, Vector2.zero);
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(surface.gameObject, gameObject.scene);
            canvas = surface.gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 15;
            var scaler = surface.gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280f, 720f);
            scaler.matchWidthOrHeight = .5f;
            fragmentIcon = Resources.Load<Texture2D>("StoryFragment");
            font = Font.CreateDynamicFontFromOSFont(new[] { "Malgun Gothic", "Noto Sans CJK KR", "Apple SD Gothic Neo" }, 18);
            ownsFont = font != null;
            if (!ownsFont) font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            var shader = Resources.Load<Shader>("ShopProductOutline");
            if (shader != null) highlightMaterial = new Material(shader);
            for (int i = 0; i < prices.Length; i++)
            {
                var root = MakeRect($"Shop price {i}", surface, Vector2.zero, new Vector2(.5f, 1f), Vector2.zero, new Vector2(62f, LabelHeight));
                var image = MakeFragmentIcon(root, Vector2.zero, new Vector2(1f, 1f), 22f);
                var amount = MakeText("Amount", root, Vector2.zero, Vector2.zero, new Vector2(23f, 0f), 18, Ink, FontStyle.Bold);
                amount.alignment = TextAnchor.MiddleLeft;
                var outline = amount.gameObject.AddComponent<Outline>();
                outline.effectColor = new Color(.035f, .02f, .055f, .95f);
                outline.effectDistance = new Vector2(1.2f, -1.2f);
                prices[i] = new PriceLabel(root, image, amount);
            }

            detail = MakeRect("Product information", surface, Vector2.zero, Vector2.zero, Vector2.zero, new Vector2(340f, 118f));
            AddImage(detail, Gold);
            var background = MakeRect("Panel", detail, Vector2.zero, Vector2.zero, Vector2.zero, Vector2.zero);
            background.anchorMax = Vector2.one;
            background.offsetMin = new Vector2(1f, 1f);
            background.offsetMax = new Vector2(-1f, -1f);
            AddImage(background, Panel);
            title = MakeText("Product name", detail, Vector2.up, Vector2.up, new Vector2(14f, -10f), 21, Ink, FontStyle.Bold);
            classification = MakeText("Classification", detail, Vector2.up, Vector2.up, new Vector2(14f, -37f), 13, Muted);
            description = MakeText("Description", detail, Vector2.up, Vector2.up, new Vector2(14f, -59f), 16, Ink);
            description.horizontalOverflow = HorizontalWrapMode.Wrap;
            description.verticalOverflow = VerticalWrapMode.Truncate;
            description.resizeTextMinSize = 11;
            description.resizeTextMaxSize = 16;
            MakeFragmentIcon(detail, Vector2.zero, new Vector2(13f, 32f), 21f);
            detailPrice = MakeText("Price", detail, Vector2.zero, Vector2.zero, new Vector2(36f, 31f), 16, Ink, FontStyle.Bold);
            purchaseHint = MakeText("Purchase hint", detail, Vector2.right, Vector2.right, new Vector2(-14f, 31f), 15, Ink);
            purchaseHint.alignment = TextAnchor.MiddleRight;
            purchaseHint.rectTransform.sizeDelta = new Vector2(138f, 24f);
            purchaseHint.text = "[F] 구매하기";
            purchaseStatus = MakeText("Purchase status", detail, Vector2.zero, Vector2.zero, new Vector2(14f, 7f), 14, Shortage);
            detail.gameObject.SetActive(false);
            Canvas.ForceUpdateCanvases();
        }

        RectTransform MakeFragmentIcon(RectTransform parent, Vector2 anchor, Vector2 offset, float size)
        {
            var rect = MakeRect("Story fragment", parent, anchor, Vector2.zero, offset, Vector2.one * size);
            var image = rect.gameObject.AddComponent<RawImage>();
            image.texture = fragmentIcon;
            image.raycastTarget = false;
            image.enabled = fragmentIcon != null;
            return rect;
        }

        Text MakeText(string label, RectTransform parent, Vector2 anchor, Vector2 pivot, Vector2 offset, int size,
            Color color, FontStyle style = FontStyle.Normal)
        {
            var rect = MakeRect(label, parent, anchor, pivot, offset, new Vector2(298f, 24f));
            var text = rect.gameObject.AddComponent<Text>();
            text.font = font;
            text.fontSize = size;
            text.fontStyle = style;
            text.color = color;
            text.raycastTarget = false;
            text.supportRichText = false;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            return text;
        }

        static RectTransform MakeRect(string name, Transform parent, Vector2 anchor, Vector2 pivot, Vector2 offset, Vector2 size)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = anchor;
            rect.pivot = pivot;
            rect.anchoredPosition = offset;
            rect.sizeDelta = size;
            return rect;
        }

        static void AddImage(RectTransform rect, Color color)
        {
            var image = rect.gameObject.AddComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
        }

        void OnDisable()
        {
            Select(null);
            if (canvas != null) canvas.enabled = false;
        }

        void OnDestroy()
        {
            if (highlightMaterial != null) Destroy(highlightMaterial);
            if (ownsFont && font != null) Destroy(font);
            if (surface != null) Destroy(surface.gameObject);
        }

        sealed class PriceLabel
        {
            internal readonly RectTransform Root;
            readonly RectTransform icon;
            readonly Text amount;
            int value = -1;
            bool sold;

            internal PriceLabel(RectTransform root, RectTransform icon, Text amount)
            {
                Root = root;
                this.icon = icon;
                this.amount = amount;
            }

            internal void Set(int price, bool affordable)
            {
                if (sold || price != value)
                {
                    sold = false;
                    value = price;
                    icon.gameObject.SetActive(true);
                    amount.alignment = TextAnchor.MiddleLeft;
                    amount.rectTransform.anchoredPosition = new Vector2(23f, 0f);
                    amount.text = price.ToString("N0");
                    float textWidth = Mathf.Ceil(amount.preferredWidth) + 2f;
                    amount.rectTransform.sizeDelta = new Vector2(textWidth, LabelHeight);
                    Root.sizeDelta = new Vector2(icon.rect.width + 2f + textWidth, LabelHeight);
                }
                amount.color = affordable ? Ink : Shortage;
            }

            internal void SetSold()
            {
                if (sold) return;
                sold = true;
                icon.gameObject.SetActive(false);
                amount.text = "품절";
                amount.color = new Color(.72f, .72f, .74f);
                amount.alignment = TextAnchor.MiddleCenter;
                amount.rectTransform.anchoredPosition = Vector2.zero;
                Root.sizeDelta = amount.rectTransform.sizeDelta = new Vector2(56f, LabelHeight);
            }
        }
    }
}
