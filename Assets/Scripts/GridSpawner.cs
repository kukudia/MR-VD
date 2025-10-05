using UnityEngine;

public class GridSpawner : MonoBehaviour
{
    // Prefab 引用
    public GameObject prefab;

    // 网格设置
    public int rows = 100; // 行数
    public int columns = 100; // 列数
    public float spacing = 1.0f; // 间距

    // 初始位置
    public Vector3 startPosition = Vector3.zero;

    void Start()
    {
        SpawnGrid();
    }

    void SpawnGrid()
    {
        if (prefab == null)
        {
            Debug.LogError("Prefab 未设置！");
            return;
        }

        for (int x = 0; x < rows; x++)
        {
            for (int y = 0; y < columns; y++)
            {
                // 计算生成位置
                Vector3 position = startPosition + new Vector3(x * spacing, y * spacing, 0);
                // 实例化Prefab
                Instantiate(prefab, position, Quaternion.identity, this.transform);
            }
        }
    }
}
