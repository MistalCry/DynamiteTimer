using System.Collections.Generic;
using UnityEngine;

public class WorldTimerRenderer : MonoBehaviour
{
    private const float NormalSmoothTime = 0.1f;
    private const float UrgentSmoothTime = 0.04f;
    private const float TeleportDistance = 120f;
    private const float LabelPaddingX = 8f;
    private const float LabelPaddingY = 4f;
    private const float OverlapSpacing = 4f;

    private readonly Dictionary<DynamiteTimer, LabelState> labelStates =
        new Dictionary<DynamiteTimer, LabelState>();

    private readonly List<DynamiteTimer> cleanupList =
        new List<DynamiteTimer>();

    private readonly List<LabelEntry> labelEntries =
        new List<LabelEntry>();

    private readonly List<Rect> occupiedRects =
        new List<Rect>();

    private GUIStyle labelStyle;
    private bool loggedMissingCamera;
    private bool loggedCamera;

    private class LabelState
    {
        public Vector2 DisplayPosition;
        public Vector2 Velocity;
        public bool Initialized;
        public bool Visible;
    }

    private struct LabelEntry
    {
        public DynamiteTimer Timer;
        public Vector2 Position;
    }

    private void Update()
    {
        Camera cam =
            ResolveCamera();

        UpdateTimers(cam);
        CleanupDeadTimers();
    }

    private void EnsureStyle()
    {
        if (labelStyle != null)
            return;

        labelStyle = new GUIStyle(GUI.skin.label);
        labelStyle.alignment = TextAnchor.MiddleCenter;
        labelStyle.normal.textColor = Color.white;
        labelStyle.fontStyle = FontStyle.Bold;
    }

    private void OnGUI()
    {
        EnsureStyle();

        Camera cam =
            ResolveCamera();

        if (cam == null)
        {
            if (!loggedMissingCamera &&
                DynamiteTimerPlugin.Log != null)
            {
                DynamiteTimerPlugin.Log.LogWarning(
                    "Dynamite timer renderer could not find Camera.main");

                loggedMissingCamera = true;
            }

            return;
        }

        if (!loggedCamera &&
            DynamiteTimerPlugin.Log != null)
        {
            DynamiteTimerPlugin.Log.LogInfo(
                "Dynamite timer renderer using camera " + cam.name);

            loggedCamera = true;
        }

        BuildLabelEntries();
        DrawLabelsWithOverlapAvoidance();
    }

    private void UpdateTimers(Camera cam)
    {
        DynamiteTimer[] timers =
            FindObjectsOfType<DynamiteTimer>();

        foreach (DynamiteTimer timer in timers)
        {
            if (timer == null)
                continue;

            LabelState state =
                GetLabelState(timer);

            float remainingTime =
                timer.RemainingTime;

            if (cam == null ||
                remainingTime <= 0)
            {
                state.Visible = false;
                continue;
            }

            Vector3 worldPos =
                GetLabelWorldPosition(timer);

            Vector3 screenPos =
                cam.WorldToScreenPoint(worldPos);

            if (screenPos.z <= 0)
            {
                state.Visible = false;
                continue;
            }

            Vector2 targetPosition =
                new Vector2(
                    screenPos.x,
                    Screen.height - screenPos.y);

            if (!state.Initialized ||
                Vector2.Distance(state.DisplayPosition, targetPosition) >
                TeleportDistance)
            {
                state.DisplayPosition = targetPosition;
                state.Velocity = Vector2.zero;
                state.Initialized = true;
            }
            else
            {
                float smoothTime =
                    remainingTime <= 1.5f
                        ? UrgentSmoothTime
                        : NormalSmoothTime;

                state.DisplayPosition =
                    Vector2.SmoothDamp(
                        state.DisplayPosition,
                        targetPosition,
                        ref state.Velocity,
                        smoothTime);
            }

            state.Visible = true;
        }
    }

    private LabelState GetLabelState(DynamiteTimer timer)
    {
        LabelState state;

        if (!labelStates.TryGetValue(timer, out state))
        {
            state = new LabelState();
            labelStates[timer] = state;
        }

        return state;
    }

    private void CleanupDeadTimers()
    {
        cleanupList.Clear();

        foreach (KeyValuePair<DynamiteTimer, LabelState> pair in labelStates)
        {
            if (pair.Key == null ||
                pair.Key.RemainingTime <= 0)
            {
                cleanupList.Add(pair.Key);
            }
        }

        foreach (DynamiteTimer timer in cleanupList)
        {
            labelStates.Remove(timer);
        }
    }

    private void BuildLabelEntries()
    {
        labelEntries.Clear();

        foreach (KeyValuePair<DynamiteTimer, LabelState> pair in labelStates)
        {
            DynamiteTimer timer =
                pair.Key;

            LabelState state =
                pair.Value;

            if (timer == null ||
                !state.Visible ||
                timer.RemainingTime <= 0)
            {
                continue;
            }

            labelEntries.Add(
                new LabelEntry
                {
                    Timer = timer,
                    Position = state.DisplayPosition
                });
        }

        labelEntries.Sort(CompareLabelEntries);
    }

    private static int CompareLabelEntries(LabelEntry a, LabelEntry b)
    {
        int result =
            a.Timer.RemainingTime.CompareTo(b.Timer.RemainingTime);

        if (result != 0)
            return result;

        result =
            a.Position.y.CompareTo(b.Position.y);

        if (result != 0)
            return result;

        return a.Position.x.CompareTo(b.Position.x);
    }

    private void DrawLabelsWithOverlapAvoidance()
    {
        occupiedRects.Clear();

        foreach (LabelEntry entry in labelEntries)
        {
            DynamiteTimer timer =
                entry.Timer;

            float remainingTime =
                timer.RemainingTime;

            string text =
                GetLabelText(timer, remainingTime);

            GUIContent content =
                new GUIContent(text);

            LabelVisual visual =
                GetLabelVisual(remainingTime);

            labelStyle.fontSize =
                visual.FontSize;

            Vector2 size =
                labelStyle.CalcSize(content);

            size.x += LabelPaddingX;
            size.y += LabelPaddingY;

            Rect rect =
                new Rect(
                    entry.Position.x - size.x * 0.5f,
                    entry.Position.y - size.y * 0.5f,
                    size.x,
                    size.y);

            rect =
                AvoidOverlap(rect);

            DrawOutlinedLabel(rect, content, visual.Color);
        }
    }

    private Rect AvoidOverlap(Rect rect)
    {
        int attempt = 0;

        while (OverlapsAny(rect) &&
            attempt < 10)
        {
            float direction =
                attempt % 2 == 0
                    ? -1f
                    : 1f;

            float distance =
                (attempt / 2 + 1) *
                (rect.height + OverlapSpacing);

            rect.y += direction * distance;
            attempt++;
        }

        occupiedRects.Add(rect);

        return rect;
    }

    private bool OverlapsAny(Rect rect)
    {
        foreach (Rect occupiedRect in occupiedRects)
        {
            if (rect.Overlaps(occupiedRect))
                return true;
        }

        return false;
    }

    private void DrawOutlinedLabel(
        Rect rect,
        GUIContent content,
        Color color)
    {
        Color originalColor =
            labelStyle.normal.textColor;

        labelStyle.normal.textColor =
            Color.black;

        GUI.Label(Offset(rect, -1f, 0f), content, labelStyle);
        GUI.Label(Offset(rect, 1f, 0f), content, labelStyle);
        GUI.Label(Offset(rect, 0f, -1f), content, labelStyle);
        GUI.Label(Offset(rect, 0f, 1f), content, labelStyle);

        labelStyle.normal.textColor =
            color;

        GUI.Label(rect, content, labelStyle);

        labelStyle.normal.textColor =
            originalColor;
    }

    private static Rect Offset(
        Rect rect,
        float x,
        float y)
    {
        rect.x += x;
        rect.y += y;

        return rect;
    }

    private static LabelVisual GetLabelVisual(float remainingTime)
    {
        if (remainingTime <= 1.5f)
        {
            return new LabelVisual
            {
                Color = new Color(1f, 0.85f, 0.1f),
                FontSize = 24
            };
        }

        if (remainingTime <= 3f)
        {
            return new LabelVisual
            {
                Color = new Color(1f, 0.85f, 0.1f),
                FontSize = 24
            };
        }

        return new LabelVisual
        {
            Color = new Color(0.35f, 1f, 0.35f),
            FontSize = 22
        };
    }

    private struct LabelVisual
    {
        public Color Color;
        public int FontSize;
    }

    private static string GetLabelText(
        DynamiteTimer timer,
        float remainingTime)
    {
        Item item =
            timer.GetComponent<Item>();

        string locationLabel =
            SlotLabelResolver.GetItemLocationLabel(item);

        if (string.IsNullOrEmpty(locationLabel))
            return remainingTime.ToString("0.0") + "s";

        return locationLabel + " " +
            remainingTime.ToString("0.0") + "s";
    }

    private static Vector3 GetLabelWorldPosition(DynamiteTimer timer)
    {
        SpriteRenderer spriteRenderer =
            timer.GetComponentInChildren<SpriteRenderer>();

        if (spriteRenderer != null)
        {
            Bounds bounds =
                spriteRenderer.bounds;

            return new Vector3(
                bounds.center.x,
                bounds.max.y + 0.25f,
                bounds.center.z);
        }

        Vector3 worldPos =
            timer.transform.position;

        worldPos.y += 0.8f;

        return worldPos;
    }

    private static Camera ResolveCamera()
    {
        if (Camera.main != null)
            return Camera.main;

        Camera[] cameras =
            Camera.allCameras;

        foreach (Camera camera in cameras)
        {
            if (camera != null &&
                camera.isActiveAndEnabled)
            {
                return camera;
            }
        }

        return null;
    }
}
