using UnityEngine;
using UnityEngine.UI;

public sealed class R01Hud : MonoBehaviour
{
    private R01GameRoot game;
    private Text titleText;
    private Text statusText;
    private Text controlsText;
    private Text messageText;
    private Text debugText;
    private GameObject debugPanel;

    public void Initialize(R01GameRoot owner)
    {
        game = owner;
        Canvas canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;
        CanvasScaler scaler = gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1280f, 720f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;
        gameObject.AddComponent<GraphicRaycaster>();

        Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        CreatePanel("Top Panel", new Vector2(18f, -16f), new Vector2(660f, 92f), new Color(0.02f, 0.04f, 0.08f, 0.82f));
        titleText = CreateText("Title", new Vector2(30f, -24f), new Vector2(620f, 28f), 22, Color.white, font, TextAnchor.UpperLeft);
        statusText = CreateText("Status", new Vector2(30f, -55f), new Vector2(620f, 28f), 16, new Color(0.78f, 0.92f, 1f, 1f), font, TextAnchor.UpperLeft);
        controlsText = CreateText("Controls", new Vector2(30f, -84f), new Vector2(940f, 28f), 15, new Color(0.72f, 0.78f, 0.88f, 1f), font, TextAnchor.UpperLeft);
        messageText = CreateText("Message", new Vector2(640f, -42f), new Vector2(600f, 64f), 18, new Color(1f, 0.9f, 0.54f, 1f), font, TextAnchor.MiddleCenter);

        debugPanel = CreatePanel("Debug Panel", new Vector2(18f, -126f), new Vector2(445f, 214f), new Color(0.02f, 0.04f, 0.08f, 0.88f));
        debugText = CreateText("Debug Text", new Vector2(30f, -138f), new Vector2(420f, 190f), 14, new Color(0.68f, 1f, 0.78f, 1f), font, TextAnchor.UpperLeft);
        debugPanel.SetActive(false);
    }

    public void Refresh()
    {
        if (game == null || game.Player == null || titleText == null)
        {
            return;
        }

        R01Player player = game.Player;
        string preset = game.MixedPreset ? "混编" : "单猫";
        string mode = game.TargetMode == R01TargetMode.DirectionPriority ? "A 方向优先" : "B 简化选敌";
        string dodge = player.IsDodgeReady ? "就绪" : $"{player.DodgeCooldownRemaining:0.0}s";
        string buff = player.IsHissBuffActive ? $"强化 {player.HissBuffRemaining:0.0}s" : "—";

        titleText.text = "R01 对峙与反攻";
        statusText.text = $"生命 {player.CurrentHealth:0}/{player.MaxHealth:0}   Q {player.HissEnergy:0}/{player.MaxHissEnergy:0}   猫扑 {dodge}   {buff}";
        controlsText.text = $"WASD 移动   Space 猫扑   Q 哈气   R 重置   Esc 暂停   F1 调试   F2 {preset}   F3 测试击退   F4 Q满/空   F5 扑就绪/冷却   Tab 自动攻击 {(game.AutoAttack ? "开" : "关")}   选敌 {mode}";
        messageText.text = game.IsPaused ? "已暂停 · 按 Esc 继续" : game.Message;

        if (debugPanel != null)
        {
            debugPanel.SetActive(game.ShowDebug);
        }

        if (debugText != null && game.ShowDebug)
        {
            R01Enemy target = player.CurrentTarget;
            debugText.text = $"调试显示\n" +
                             $"场景时间：{game.ElapsedTime:0.0}s\n" +
                             $"预设：{preset}   存活敌人：{game.AliveEnemyCount}\n" +
                             $"当前目标：{(target == null ? "无" : target.name)}\n" +
                             $"目标状态：{(target == null ? "—" : target.StateLabel)}\n" +
                             $"目标生命：{(target == null ? "—" : $"{target.CurrentHealth:0}/{target.MaxHealth:0}")}\n" +
                             $"玩家位置：{player.Position.x:0.00}, {player.Position.y:0.00}\n" +
                             $"测试：Q {(game.StartingHissEnergy > 0f ? "满" : "空")}；猫扑 {(game.StartWithDodgeOnCooldown ? "冷却" : "就绪")}\n" +
                             $"提示：黄色观察／红色锁向；锁向后普通击退不生效。";
        }
    }

    private GameObject CreatePanel(string name, Vector2 anchoredPosition, Vector2 size, Color color)
    {
        GameObject panelObject = new GameObject(name);
        panelObject.transform.SetParent(transform, false);
        RectTransform rect = panelObject.AddComponent<RectTransform>();
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = size;
        Image image = panelObject.AddComponent<Image>();
        image.color = color;
        return panelObject;
    }

    private Text CreateText(string name, Vector2 anchoredPosition, Vector2 size, int fontSize, Color color, Font font, TextAnchor alignment)
    {
        GameObject textObject = new GameObject(name);
        textObject.transform.SetParent(transform, false);
        RectTransform rect = textObject.AddComponent<RectTransform>();
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = size;

        Text text = textObject.AddComponent<Text>();
        text.font = font;
        text.fontSize = fontSize;
        text.color = color;
        text.alignment = alignment;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Overflow;
        text.raycastTarget = false;
        return text;
    }
}
