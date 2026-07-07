using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Sol
{
    public class Dun_Gen2D : MonoBehaviour
    {
        [Header("Room Prefab")]
        [Tooltip("2D room prefab with a Room2D component.")]
        [SerializeField] private GameObject roomPrefab;

        [Header("Maze Size")]
        [Tooltip("Number of rooms along X.")]
        [SerializeField] private int numX = 10;

        [Tooltip("Number of rooms along Y.")]
        [SerializeField] private int numY = 10;

        // Dungeon grid.
        private Room2D[,] rooms;
        private readonly Stack<Room2D> stack = new Stack<Room2D>();

        // Room W and H.
        private float roomWidth;
        private float roomHeight;

        private bool generating;

        private void Start()
        {
            GetRoomSize();
            SetCamera();
            BuildRoomGrid();
            SetCamera();
        }

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.R) && !generating)
            {
                CreateDungeon();
            }
        }

        public void CreateDungeon()
        {
            if (generating)
            {
                return;
            }

            Reset();
            RemoveRoomWall(0, 0, Room2D.Directions.SOUTH);
            RemoveRoomWall(numX - 1, numY - 1, Room2D.Directions.EAST);

            stack.Push(rooms[0, 0]);
            StartCoroutine(Coroutine_DunGen());
        }

        private void GetRoomSize()
        {
            SpriteRenderer[] spriteRenderers =
                roomPrefab.GetComponentsInChildren<SpriteRenderer>();

            Vector3 minBounds = Vector3.positiveInfinity;
            Vector3 maxBounds = Vector3.negativeInfinity;

            foreach (SpriteRenderer ren in spriteRenderers)
            {
                minBounds = Vector3.Min(minBounds, ren.bounds.min);
                maxBounds = Vector3.Max(maxBounds, ren.bounds.max);
            }

            roomWidth = maxBounds.x - minBounds.x;
            roomHeight = maxBounds.y - minBounds.y;
        }

        private void SetCamera()
        {
            Camera.main.transform.position = new Vector3(
                numX * (roomWidth - 1) / 2,
                numY * (roomHeight - 1) / 2,
                -100f);

            float min_value = Mathf.Min(numX * (roomWidth - 1), numY * (roomHeight - 1));
            Camera.main.orthographicSize = min_value * 0.75f;
        }

        private void BuildRoomGrid()
        {
            rooms = new Room2D[numX, numY];

            for (int i = 0; i < numY; ++i)
            {
                for (int j = 0; j < numX; ++j)
                {
                    GameObject room = Instantiate(
                        roomPrefab,
                        new Vector3(i * roomWidth, j * roomHeight, 0f),
                        Quaternion.identity);

                    room.name = "Room_" + i.ToString() + "_" + j.ToString();
                    rooms[i, j] = room.GetComponent<Room2D>();
                    rooms[i, j].Index = new Vector2Int(i, j);
                }
            }
        }

        private void RemoveRoomWall(int x, int y, Room2D.Directions dir)
        {
            if (dir != Room2D.Directions.NONE)
            {
                rooms[x, y].SetDirFlag(dir, false);
            }

            Room2D.Directions opp = Room2D.Directions.NONE;
            switch (dir)
            {
                case Room2D.Directions.NORTH:
                    if (y < numY - 1)
                    {
                        opp = Room2D.Directions.SOUTH;
                        ++y;
                    }
                    break;

                case Room2D.Directions.EAST:
                    if (x < numX - 1)
                    {
                        opp = Room2D.Directions.WEST;
                        ++x;
                    }
                    break;

                case Room2D.Directions.SOUTH:
                    if (y > 0)
                    {
                        opp = Room2D.Directions.NORTH;
                        --y;
                    }
                    break;

                case Room2D.Directions.WEST:
                    if (x > 0)
                    {
                        opp = Room2D.Directions.EAST;
                        --x;
                    }
                    break;
            }

            if (opp != Room2D.Directions.NONE)
            {
                rooms[x, y].SetDirFlag(opp, false);
            }
        }

        public List<Tuple<Room2D.Directions, Room2D>> GetUnvisitedNeighbors(int cx, int cy)
        {
            List<Tuple<Room2D.Directions, Room2D>> neighbours =
                new List<Tuple<Room2D.Directions, Room2D>>();

            foreach (Room2D.Directions dir in Enum.GetValues(typeof(Room2D.Directions)))
            {
                int x = cx;
                int y = cy;

                switch (dir)
                {
                    case Room2D.Directions.NORTH:
                        if (y < numY - 1)
                        {
                            ++y;
                            if (!rooms[x, y].visited)
                            {
                                neighbours.Add(new Tuple<Room2D.Directions, Room2D>(
                                    Room2D.Directions.NORTH,
                                    rooms[x, y]));
                            }
                        }
                        break;

                    case Room2D.Directions.EAST:
                        if (x < numX - 1)
                        {
                            ++x;
                            if (!rooms[x, y].visited)
                            {
                                neighbours.Add(new Tuple<Room2D.Directions, Room2D>(
                                    Room2D.Directions.EAST,
                                    rooms[x, y]));
                            }
                        }
                        break;

                    case Room2D.Directions.SOUTH:
                        if (y > 0)
                        {
                            --y;
                            if (!rooms[x, y].visited)
                            {
                                neighbours.Add(new Tuple<Room2D.Directions, Room2D>(
                                    Room2D.Directions.SOUTH,
                                    rooms[x, y]));
                            }
                        }
                        break;

                    case Room2D.Directions.WEST:
                        if (x > 0)
                        {
                            --x;
                            if (!rooms[x, y].visited)
                            {
                                neighbours.Add(new Tuple<Room2D.Directions, Room2D>(
                                    Room2D.Directions.WEST,
                                    rooms[x, y]));
                            }
                        }
                        break;
                }
            }

            return neighbours;
        }

        private bool GenerateStep()
        {
            if (stack.Count == 0)
            {
                return true;
            }

            Room2D r = stack.Peek();
            var neighbours = GetUnvisitedNeighbors(r.Index.x, r.Index.y);

            if (neighbours.Count != 0)
            {
                int index = neighbours.Count > 1 ? UnityEngine.Random.Range(0, neighbours.Count) : 0;
                var item = neighbours[index];
                Room2D neighbour = item.Item2;

                neighbour.visited = true;
                RemoveRoomWall(r.Index.x, r.Index.y, item.Item1);
                stack.Push(neighbour);
            }
            else
            {
                stack.Pop();
            }

            return false;
        }

        private IEnumerator Coroutine_DunGen()
        {
            generating = true;
            bool flag = false;

            while (!flag)
            {
                flag = GenerateStep();
                yield return new WaitForSeconds(0.05f);
            }

            generating = false;
        }

        private void Reset()
        {
            for (int i = 0; i < numX; ++i)
            {
                for (int j = 0; j < numY; ++j)
                {
                    rooms[i, j].visited = false;
                    rooms[i, j].SetDirFlag(Room2D.Directions.NORTH, true);
                    rooms[i, j].SetDirFlag(Room2D.Directions.SOUTH, true);
                    rooms[i, j].SetDirFlag(Room2D.Directions.EAST, true);
                    rooms[i, j].SetDirFlag(Room2D.Directions.WEST, true);
                    rooms[i, j].visited = false;
                }
            }
        }
    }
}
