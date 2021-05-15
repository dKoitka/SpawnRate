using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class UIBehaviour : MonoBehaviour
{
    [SerializeField] TMPro.TextMeshProUGUI m_textObject;

    TimePercentageData m_percentageReached;
    bool m_refreshUI = false;

    private void Awake()
    {
        SpawnBehaviour.PercentageReached_Event += SpawnBehaviour_PercentageReached_Event;

        RefreshUI();
    }

    private void SpawnBehaviour_PercentageReached_Event(TimePercentageData obj)
    {
        m_percentageReached = obj;
        m_refreshUI = true;
    }

    private void Update()
    {
        if (m_refreshUI)
        {
            RefreshUI();
            m_refreshUI = false;
        }
    }

    void RefreshUI()
    {
        m_textObject.text = 
            $"Num Spawned: {m_percentageReached.m_numItemsSpawned}\nArea Covered: {m_percentageReached.m_percentageArea.ToString("0.00")}%\n\nTime Covered%: {m_percentageReached.m_percentageTime.ToString("0.00")}%\nTime Covered: {m_percentageReached.m_actualTime.ToString("0.00")}s";
    }

    private void OnDestroy()
    {
        SpawnBehaviour.PercentageReached_Event -= SpawnBehaviour_PercentageReached_Event;
    }

}
