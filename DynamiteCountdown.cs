using System.Collections.Generic;
using UnityEngine;

public class DynamiteCountdown : MonoBehaviour
{
    public static Dictionary<Item, float> ActiveDynamites =
        new Dictionary<Item, float>();

    private void OnGUI()
    {
        float y = 10;

        List<Item> removeList = new List<Item>();

        foreach (var pair in ActiveDynamites)
        {
            Item item = pair.Key;

            float remain =
                pair.Value - Time.time;

            if (item == null || remain <= 0)
            {
                removeList.Add(item);
                continue;
            }

            GUI.Label(
                new Rect(10, y, 300, 20),
                $"{item.name}: {remain:0.0}s");

            y += 25;
        }

        foreach (var item in removeList)
        {
            ActiveDynamites.Remove(item);
        }
    }
}