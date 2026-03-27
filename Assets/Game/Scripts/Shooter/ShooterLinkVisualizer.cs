using UnityEngine;
using System.Collections.Generic;

public class ShooterLinkVisualizer : MonoBehaviour
{
    public Material lineMaterial;
    public float lineWidth = 0.06f;
    public Vector3 shooterPointOffset = new Vector3(0f, 0.15f, 0f);

    private readonly Dictionary<int, List<Shooter>> groups = new Dictionary<int, List<Shooter>>();
    private readonly Dictionary<int, List<LinkPair>> groupLinks = new Dictionary<int, List<LinkPair>>();

    private class LinkPair
    {
        public LineRenderer firstHalf;
        public LineRenderer secondHalf;
    }

    private void LateUpdate()
    {
        RebuildGroups();
        UpdateLinks();
    }

    private void RebuildGroups()
    {
        groups.Clear();

        Shooter[] shooters = FindObjectsOfType<Shooter>(true);

        for (int i = 0; i < shooters.Length; i++)
        {
            Shooter s = shooters[i];
            if (s == null || !s.IsAlive)
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

    private void UpdateLinks()
    {
        List<int> existingKeys = new List<int>(groupLinks.Keys);

        for (int i = 0; i < existingKeys.Count; i++)
        {
            int gid = existingKeys[i];
            if (!groups.ContainsKey(gid))
            {
                DisableGroup(gid);
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

            SortMembersForStableLinks(members);

            int neededLinkCount = members.Count - 1;
            EnsureGroupLinks(gid, neededLinkCount);

            List<LinkPair> links = groupLinks[gid];

            for (int i = 0; i < links.Count; i++)
            {
                bool shouldEnable = i < neededLinkCount;

                if (!shouldEnable)
                {
                    SetPairEnabled(links[i], false);
                    continue;
                }

                Shooter a = members[i];
                Shooter b = members[i + 1];

                if (a == null || b == null)
                {
                    SetPairEnabled(links[i], false);
                    continue;
                }

                Vector3 p0 = a.transform.position + shooterPointOffset;
                Vector3 p1 = b.transform.position + shooterPointOffset;
                Vector3 mid = (p0 + p1) * 0.5f;

                LinkPair pair = links[i];
                SetPairEnabled(pair, true);

                SetupHalf(pair.firstHalf, p0, mid, a.linkColor);
                SetupHalf(pair.secondHalf, mid, p1, b.linkColor);
            }
        }
    }

    private void SortMembersForStableLinks(List<Shooter> members)
    {
        members.Sort((a, b) =>
        {
            if (a == null && b == null) return 0;
            if (a == null) return 1;
            if (b == null) return -1;

            return a.transform.position.x.CompareTo(b.transform.position.x);
        });
    }

    private void EnsureGroupLinks(int gid, int needed)
    {
        if (!groupLinks.ContainsKey(gid))
        {
            groupLinks[gid] = new List<LinkPair>();
        }

        List<LinkPair> list = groupLinks[gid];

        while (list.Count < needed)
        {
            LinkPair pair = new LinkPair();
            pair.firstHalf = CreateLineRenderer("LinkLine_" + gid + "_" + list.Count + "_A");
            pair.secondHalf = CreateLineRenderer("LinkLine_" + gid + "_" + list.Count + "_B");
            list.Add(pair);
        }

        for (int i = needed; i < list.Count; i++)
        {
            SetPairEnabled(list[i], false);
        }
    }

    private LineRenderer CreateLineRenderer(string objName)
    {
        GameObject go = new GameObject(objName);
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

        return lr;
    }

    private void SetupHalf(LineRenderer lr, Vector3 start, Vector3 end, Color color)
    {
        if (lr == null)
        {
            return;
        }

        lr.positionCount = 2;
        lr.SetPosition(0, start);
        lr.SetPosition(1, end);
        lr.startColor = color;
        lr.endColor = color;
    }

    private void SetPairEnabled(LinkPair pair, bool enabled)
    {
        if (pair == null)
        {
            return;
        }

        if (pair.firstHalf != null)
        {
            pair.firstHalf.enabled = enabled;
        }

        if (pair.secondHalf != null)
        {
            pair.secondHalf.enabled = enabled;
        }
    }

    private void DisableGroup(int gid)
    {
        if (!groupLinks.ContainsKey(gid))
        {
            return;
        }

        List<LinkPair> list = groupLinks[gid];
        for (int i = 0; i < list.Count; i++)
        {
            SetPairEnabled(list[i], false);
        }
    }
}