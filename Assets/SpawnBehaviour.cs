using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 
/// With the supplied Animation curve, we want to know how many units to spawn at a point in time.
/// The value received from AnimationCurve.Evaluate will be the rate of spawning, not the number to spawn, so we'll calculate the area under the curve up to that point.
/// The area will tell us how many units to spawn as we can normalise it from the total area.
/// 
/// To find the area under a curve, we must split the curve into equal segments (the shape will be a trapezoid), calculate each segments' area, and sum them together. Precision is key as the wider the 
/// segments are, the less acurrate the end result will be.
/// 
/// How to calculate segments:
/// 
/// 
/// |\              
/// | \            /|
/// |  \          / |
/// |   \        /  |
/// |    \ _____/   |
/// |     |     |   |
/// |     |     |   |
/// |     |     |   |
/// |_____|_____|___|
/// 
/// At equal intervals, we'll grab the y value from 2 points in the curve and generate a trapezoid, which can be calculated by finding the mid height and multiplying it by the width. 
/// 
/// If we create small segments we can get an accurate approximation of the area under the curve. 
/// 
/// ~ Daniel Koitka
/// </summary>




//extension methods for making the code look a little neater 
static class AnimationCurveHelper
{
    //gets size of a segment
    public static float GetAreaUnderCurve(this AnimationCurve animCurve, float section1, float section2)
    {
        float delta = (section2 - section1);
        float section1Value = animCurve.Evaluate(section1);
        float section2Value = animCurve.Evaluate(section2);
        return (section1Value + section2Value) * 0.5f * delta;
    }

    //gets the size of the area by splitting the curve into segments
    public static float GetAreaUnderCurve(this AnimationCurve animCurve, float startPoint, float endPoint, float segmentSize, float timeScale)
    {
        int numSections = Mathf.CeilToInt((endPoint - startPoint) / segmentSize);

        float section1 = startPoint;
        float section2 = Mathf.Min(segmentSize, endPoint);

        float area = 0;

        while (section1 < section2)
        {
            area += animCurve.GetAreaUnderCurve(section1, section2);

            section1 = section2;
            section2 += segmentSize / timeScale;

            section2 = Mathf.Clamp(section2, 0, endPoint);
        }

        return area;
    }

    //to know the size of the curve
    public static float GetLastKeyTime(this AnimationCurve animCurve)
    {
        return animCurve.keys[animCurve.keys.Length - 1].time;
    }
}

public struct TimePercentageData
{
    public float m_percentageTime;
    public float m_percentageArea;
    public float m_actualTime;
    public int m_numItemsSpawned;
}
public class SpawnBehaviour : MonoBehaviour
{
    //tell the UI what's happening
    public static event System.Action<TimePercentageData> PercentageReached_Event;

    [SerializeField] GameObject m_objectToSpawn = null;

    [Header ("Spawning Settings")]
    [SerializeField] AnimationCurve m_spawnRateCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);
    [SerializeField] int m_maxNumItems = 50;

    [SerializeField] Vector3 m_spawnSize = new Vector3(3.0f, 3.0f, 3.0f);

    [SerializeField] float m_timeSpan = 10.0f;

    float m_areaCovered = 0.0f;
    float m_totalArea = 0.0f;

    float m_currentTime = 0.0f;
    float m_lastTime = 0.0f;
    float m_trueTime = 0.0f;

    int m_generatedObjectCount = 0;

    bool m_broadcastChanges = false;

    private void Awake()
    {
        CalculateTotalArea();
    }
    void SpawnItem(Vector3 location)
    {
        GameObject spawnedObject = Instantiate<GameObject>(m_objectToSpawn);
        //cleanup after x seconds
        GameObject.Destroy(spawnedObject, 10.0f);

        spawnedObject.GetComponent<Rigidbody>().velocity = Vector3.forward * 100;

        spawnedObject.transform.position = location;

        ++m_generatedObjectCount;
    }

    void SpawnItems(int numItems)
    {
        for (; m_generatedObjectCount < numItems;)
        {
            //generate at random position and fling it out
            Vector3 newPoint;
            newPoint.x = Random.Range(0, m_spawnSize.x);
            newPoint.y = Random.Range(0, m_spawnSize.y);
            newPoint.z = Random.Range(0, m_spawnSize.z);

            newPoint -= m_spawnSize / 2;

            //make spawn appear around us
            newPoint += transform.position;

            SpawnItem(newPoint);
        }
    }

    void CalculateTotalArea()
    {
        //get total area using trapezoids

        //we'll get it as every 60th of a second, so find how many sections we want
        //closer the sections are, the more accurate we'll have for our time
        float sectionWidth = 1.0f / 60.0f;

        float endTime = m_spawnRateCurve.GetLastKeyTime();

        m_totalArea = m_spawnRateCurve.GetAreaUnderCurve(0, endTime, sectionWidth, m_timeSpan);
    }


    void Update()
    {
        HandleSpawning();

        if (m_broadcastChanges)
        {
            m_broadcastChanges = false;
            UpdateUI();
        }
    }

    void HandleSpawning()
    {
        m_lastTime = m_currentTime;
        m_currentTime += Time.deltaTime / m_timeSpan;

        m_currentTime = Mathf.Clamp(m_currentTime, 0, m_spawnRateCurve.GetLastKeyTime());

        if (m_currentTime != m_lastTime)
        {
            m_trueTime += Time.deltaTime;

            //we could just calculate from the start, which would be more accurate, but by calculating it every frame, we can save some processing. 
            //For short spans of time, it'll be fine to calculate all the way from the start, but this code may be extracted for long periods of time
            //so this example will be culumating the area
            float areaCoveredThisFrame = m_spawnRateCurve.GetAreaUnderCurve(m_lastTime, m_currentTime);

            m_areaCovered += areaCoveredThisFrame;

            float normalisedArea = Mathf.Clamp01(m_areaCovered / m_totalArea);

            int targetNumberOfEnemies = Mathf.RoundToInt(normalisedArea * m_maxNumItems);

            SpawnItems(targetNumberOfEnemies);

            m_broadcastChanges = true;
        }
    }

    void UpdateUI()
    {
        //tell UI what's happening
        TimePercentageData data;
        data.m_percentageArea = Mathf.Clamp01(m_areaCovered / m_totalArea) * 100.0f;
        data.m_percentageTime = 100 * m_currentTime / m_spawnRateCurve.GetLastKeyTime();
        data.m_actualTime = m_trueTime;
        data.m_numItemsSpawned = m_generatedObjectCount;

        PercentageReached_Event?.Invoke(data);
    }
}
