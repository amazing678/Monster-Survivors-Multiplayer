using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace OctoberStudio
{
    public class StageChunkBehavior : MonoBehaviour
    {
        [SerializeField] SpriteRenderer sprite;
        public Vector2 Size => sprite.size;

        // IsVisible 不需要改，Unity自带的多摄像机支持会让它自动识别 P1 和 P2 的摄像机
        //public bool IsVisible => sprite.isVisible;
        public bool IsVisible
        {
            get
            {
                // 1. 如果摄像机正在看着它，绝对保留
                if (sprite.isVisible) return true;

                // 2. 如果摄像机暂时没看着它（比如刚刚提前生成在屏幕外），加一层距离保护锁
                // 保护范围比生成范围 (Margin) 稍微大一点，形成一个安全的缓冲带
                float keepAliveX = MarginX * 1.5f + Size.x;
                float keepAliveY = MarginY * 1.5f + Size.y;

                foreach (var p in PlayerBehavior.ActivePlayers)
                {
                    if (p == null || p.healthbar.IsZero) continue;

                    float distX = Mathf.Abs(transform.position.x - p.transform.position.x);
                    float distY = Mathf.Abs(transform.position.y - p.transform.position.y);

                    // 只要离任何一个玩家在安全缓冲带内，就强制保活，不许销毁！
                    if (distX < keepAliveX && distY < keepAliveY)
                    {
                        return true;
                    }
                }
                return false;
            }
        }

        public float LeftBound => transform.position.x - Size.x / 2;
        public float RightBound => transform.position.x + Size.x / 2;
        public float TopBound => transform.position.y + Size.y / 2;
        public float BottomBound => transform.position.y - Size.y / 2;

        // ==========================================
        // 修改核心：动态计算距离，遍历所有活着的玩家
        // ==========================================

        // 根据摄像机视野动态计算一个“安全缓冲距离”，保证在玩家看到虚空之前就生成地图
        private float MarginX => Camera.main != null ? Camera.main.orthographicSize * 2.5f : 15f;
        private float MarginY => Camera.main != null ? Camera.main.orthographicSize * 1.5f : 10f;

        public bool HasEmptyLeft
        {
            get
            {
                foreach (var p in PlayerBehavior.ActivePlayers)
                {
                    if (p == null || p.healthbar.IsZero) continue;
                    // 如果地块左边缘 大于 任意玩家的X坐标减去视野，说明左边该铺路了
                    if (LeftBound > p.transform.position.x - MarginX) return true;
                }
                return false;
            }
        }

        public bool HasEmptyRight
        {
            get
            {
                foreach (var p in PlayerBehavior.ActivePlayers)
                {
                    if (p == null || p.healthbar.IsZero) continue;
                    if (RightBound < p.transform.position.x + MarginX) return true;
                }
                return false;
            }
        }

        public bool HasEmptyTop
        {
            get
            {
                foreach (var p in PlayerBehavior.ActivePlayers)
                {
                    if (p == null || p.healthbar.IsZero) continue;
                    if (TopBound < p.transform.position.y + MarginY) return true;
                }
                return false;
            }
        }

        public bool HasEmptyBottom
        {
            get
            {
                foreach (var p in PlayerBehavior.ActivePlayers)
                {
                    if (p == null || p.healthbar.IsZero) continue;
                    if (BottomBound > p.transform.position.y - MarginY) return true;
                }
                return false;
            }
        }
        // ==========================================

        private List<Transform> borders = new List<Transform>();
        private List<PropBehavior> prop = new List<PropBehavior>();

        public void AddBorder(Transform border)
        {
            borders.Add(border);
        }

        public void AddProp(PropBehavior propObject)
        {
            prop.Add(propObject);
            propObject.transform.position = new Vector3(Random.Range(LeftBound, RightBound), Random.Range(BottomBound, TopBound), 0);
        }

        public void RemovePropFromBossFence(BossFenceBehavior fence)
        {
            for (int i = 0; i < prop.Count; i++)
            {
                if (fence.ValidatePosition(prop[i].transform.position, Vector2.zero))
                {
                    prop[i].Dissolve();
                    prop.RemoveAt(i);
                    i--;
                }
            }
        }

        public void Clear()
        {
            for (int i = 0; i < borders.Count; i++)
            {
                borders[i].gameObject.SetActive(false);
            }

            for (int i = 0; i < prop.Count; i++)
            {
                prop[i].gameObject.SetActive(false);
            }

            borders.Clear();
            prop.Clear();

            gameObject.SetActive(false);
        }
    }
}
