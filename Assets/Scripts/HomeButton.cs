using UnityEngine;

public class HomeButton : MonoBehaviour
{
    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape))
            GoHome();
    }

    public void GoHome()
    {
        AudioManager.PlayClick();
        SceneTransitionManager.LoadScene("MainMenu");
    }
}
