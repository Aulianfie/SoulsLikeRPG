using UnityEngine;

[DisallowMultipleComponent]
public sealed class BlessingContentController : MonoBehaviour
{
    [SerializeField] private GameObject _emptyPage;
    [SerializeField] private GameObject[] _pages;
    public BlessingPage CurrentPage { get; private set; } = BlessingPage.None;

    public void Show(BlessingPage page)
    {
        CurrentPage = page;
        _emptyPage.SetActive(page == BlessingPage.None);
        for (int i = 0; i < _pages.Length; i++)
            _pages[i].SetActive(i == (int)page);
    }
}
