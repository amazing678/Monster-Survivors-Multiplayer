using UnityEngine;
using UnityEngine.UI;

public class ImageCarousel : MonoBehaviour
{
    [Header("轮播图片")]
    public Sprite[] carouselImages;  // 存储所有轮播图片

    [Header("UI组件")]
    public Image displayImage;       // 显示当前图片的Image组件
    public Button leftButton;        // 左按钮
    public Button rightButton;       // 右按钮

    private int currentIndex = 0;    // 当前显示图片的索引

    void Start()
    {
        // 确保有图片可以显示
        if (carouselImages != null && carouselImages.Length > 0)
        {
            ShowCurrentImage();
        }
        else
        {
            Debug.LogError("请在Inspector中添加轮播图片！");
        }

        // 绑定按钮事件
        leftButton.onClick.AddListener(ShowPreviousImage);
        rightButton.onClick.AddListener(ShowNextImage);
    }

    /// <summary>
    /// 显示当前索引对应的图片
    /// </summary>
    private void ShowCurrentImage()
    {
        if (displayImage != null && currentIndex >= 0 && currentIndex < carouselImages.Length)
        {
            displayImage.sprite = carouselImages[currentIndex];
        }
    }

    /// <summary>
    /// 显示上一张图片
    /// </summary>
    public void ShowPreviousImage()
    {
        currentIndex--;
        // 如果已经是第一张，循环到最后一张
        if (currentIndex < 0)
        {
            currentIndex = carouselImages.Length - 1;
        }
        ShowCurrentImage();
    }

    /// <summary>
    /// 显示下一张图片
    /// </summary>
    public void ShowNextImage()
    {
        currentIndex++;
        // 如果已经是最后一张，循环到第一张
        if (currentIndex >= carouselImages.Length)
        {
            currentIndex = 0;
        }
        ShowCurrentImage();
    }
}
