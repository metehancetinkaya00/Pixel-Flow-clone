using UnityEngine;
using System.Collections.Generic;

public class ShooterLinkVisualizer : MonoBehaviour
{
    public Material lineMaterial;
    public float lineWidth = 0.06f;
    public Vector3 shooterPointOffset = new Vector3(0f, 0.15f, 0f);
    public Vector3 hubOffset = new Vector3(0f, 0.05f, 0f);
    public bool showHub = false;
    public float hubSphereScale = 0.12f;

    private readonly Dictionary<int, List<Shooter>> groups = new Dictionary<int, List<Shooter>>();
    private readonly Dictionary<int, List<LineRenderer>> groupLines = new Dictionary<int, List<LineRenderer>>();
    private readonly Dictionary<int, GameObject> hubObjects = new Dictionary<int, GameObject>();

    private void LateUpdate()
    {
        RebuildGroups();
        UpdateLines();
    }

    private void RebuildGroups()
    {
        groups.Clear();

        Shooter[] shooters = FindObjectsOfType<Shooter>(true);

        for (int i = 0; i < shooters.Length; i++)
        {
            Shooter s = shooters[i];
            if (s == null)
            {
                continue;
            }

            if (!s.IsAlive)
            {
                continue;
            }

            int gid = s.linkGroupId;
            if (gid <= 0)
            {
                continue;
            }

            if (!groups.ContainsKey(gid))
            {
                groups[gid] = new List<Shooter>();
            }

            groups[gid].Add(s);
        }
    }

    private void UpdateLines()
    {
        List<int> existingKeys = new List<int>(groupLines.Keys);

        for (int i = 0; i < existingKeys.Count; i++)
        {
            int k = existingKeys[i];
            if (!groups.ContainsKey(k))
            {
                DisableGroup(k);
            }
        }

        foreach (var kv in groups)
        {
            int gid = kv.Key;
            List<Shooter> members = kv.Value;

            if (members == null || members.Count < 2)
            {
                DisableGroup(gid);
                continue;
            }

            Vector3 hub = ComputeHub(members);

            EnsureGroupLines(gid, members.Count);

            List<LineRenderer> lines = groupLines[gid];

            for (int i = 0; i < lines.Count; i++)
            {
                LineRenderer lr = lines[i];
                if (lr == null)
                {
                    continue;
                }

                Shooter s = members[i];
                if (s == null)
                {
                    lr.enabled = false;
                    continue;
                }

                lr.enabled = true;

                Vector3 p0 = s.transform.position + shooterPointOffset;
                Vector3 p1 = hub;

                lr.positionCount = 2;
                lr.SetPosition(0, p0);
                lr.SetPosition(1, p1);
            }

            UpdateHubObject(gid, hub);
        }
    }

    private Vector3 ComputeHub(List<Shooter> members)
    {
        Vector3 sum = Vector3.zero;
        int count = 0;

        for (int i = 0; i < members.Count; i++)
        {
            Shooter s = members[i];
            if (s == null)
            {
                continue;
            }

            sum += (s.transform.position + shooterPointOffset);
            count += 1;
        }

        if (count <= 0)
        {
            return transform.position;
        }

        Vector3 hub = (sum / count) + hubOffset;
        return hub;
    }

    private void EnsureGroupLines(int gid, int needed)
    {
        if (!groupLines.ContainsKey(gid))
        {
            groupLines[gid] = new List<LineRenderer>();
        }

        List<LineRenderer> list = groupLines[gid];

        while (list.Count < needed)
        {
            GameObject go = new GameObject("LinkLine_" + gid.ToString() + "_" + list.Count.ToString());
            go.transform.SetParent(transform);

            LineRenderer lr = go.AddComponent<LineRenderer>();
            lr.useWorldSpace = true;
            lr.material = lineMaterial;
            lr.startWidth = lineWidth;
            lr.endWidth = lineWidth;
            lr.numCapVertices = 6;
            lr.numCornerVertices = 2;
            lr.positionCount = 2;
            lr.enabled = false;

            list.Add(lr);
        }

        for (int i = needed; i < list.Count; i++)
        {
            if (list[i] != null)
            {
                list[i].enabled = false;
            }
        }
    }

    private void DisableGroup(int gid)
    {
        if (groupLines.ContainsKey(gid))
        {
            List<LineRenderer> list = groupLines[gid];

            for (int i = 0; i < list.Count; i++)
            {
                if (list[i] != null)
                {
                    list[i].enabled = false;
                }
            }
        }

        if (hubObjects.ContainsKey(gid))
        {
            if (hubObjects[gid] != null)
            {
                hubObjects[gid].SetActive(false);
            }
        }
    }

    private void UpdateHubObject(int gid, Vector3 hubPos)
    {
        if (!showHub)
        {
            if (hubObjects.ContainsKey(gid))
            {
                if (hubObjects[gid] != null)
                {
                    hubObjects[gid].SetActive(false);
                }
            }

            return;
        }

        if (!hubObjects.ContainsKey(gid) || hubObjects[gid] == null)
        {
            GameObject hub = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            hub.name = "LinkHub_" + gid.ToString();
            hub.transform.SetParent(transform);
            hub.transform.localScale = Vector3.one * hubSphereScale;

            Collider c = hub.GetComponent<Collider>();
            if (c != null)
            {
                c.enabled = false;
            }

            hubObjects[gid] = hub;
        }

        GameObject obj = hubObjects[gid];
        obj.SetActive(true);
        obj.transform.position = hubPos;
    }
}