using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

// A spatial field sampled by grid cell. Each cell points to the next visible
// corner on a complete NavMesh route, rather than seeking the goal through walls.
public class NavMeshFlowField : BaseFlowField
{
    public float cellSize = 1.25f;
    public float cacheLifetime = 1f;
    private struct Cell { public Vector3 nextCorner; public float expires; public bool valid; }
    private readonly Dictionary<Vector3Int, Cell> cells = new Dictionary<Vector3Int, Cell>();
    private Vector3 destination;
    private SteeringAgent owner;
    private bool configured;

    public void Configure(SteeringAgent agent, Vector3 goal)
    {
        if (owner != agent || !configured || (destination - goal).sqrMagnitude > 0.16f) Invalidate();
        owner = agent;
        destination = goal;
        configured = true;
    }
    public void Invalidate() { cells.Clear(); }
    void OnDisable() { Invalidate(); configured = false; }

    public override Vector3 GetDirectionAtPosition(Vector3 position)
    {
        if (!configured || owner == null || !owner.IsOnNavigation) return Vector3.zero;
        float size = Mathf.Max(0.4f, cellSize);
        Vector3Int key = new Vector3Int(Mathf.FloorToInt(position.x / size), Mathf.FloorToInt(position.y / size), Mathf.FloorToInt(position.z / size));
        if (!cells.TryGetValue(key, out Cell cell) || Time.time >= cell.expires
            || (cell.valid && SteeringAgent.PlanarDistance(position, cell.nextCorner) < 0.35f))
        {
            cell = new Cell { expires = Time.time + Mathf.Max(0.1f, cacheLifetime), valid = false };
            Vector3 center = new Vector3((key.x + 0.5f) * size, position.y, (key.z + 0.5f) * size);
            if (owner.TrySamplePoint(center, out Vector3 source, size)
                && owner.TrySamplePoint(destination, out Vector3 target))
            {
                NavMeshPath path = new NavMeshPath();
                if (NavMesh.CalculatePath(source, target, NavMesh.AllAreas, path) && path.status == NavMeshPathStatus.PathComplete)
                {
                    Vector3[] corners = path.corners;
                    bool inRoom = corners.Length > 0;
                    foreach (Vector3 corner in corners) inRoom &= owner.IsInsideRoom(corner, owner.Radius * 0.5f);
                    if (inRoom)
                    {
                        int next = corners.Length > 1 ? 1 : 0;
                        while (next < corners.Length - 1 && SteeringAgent.PlanarDistance(position, corners[next]) < 0.4f) next++;
                        cell.nextCorner = corners[next];
                        cell.valid = true;
                    }
                }
            }
            if (cells.Count > 256) cells.Clear();
            cells[key] = cell;
        }
        if (!cell.valid) return Vector3.zero;
        // A cell can straddle an obstacle. Never steer across that obstacle just
        // because its center has a valid route on the other side.
        if (NavMesh.Raycast(position, cell.nextCorner, out NavMeshHit _, NavMesh.AllAreas)
            || !owner.IsSegmentClear(position, cell.nextCorner)) return owner.GetRouteDesiredVelocity();
        return cell.nextCorner - position;
    }
}
