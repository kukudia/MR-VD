using UnityEngine;
using UnityEngine.SceneManagement;

public class DigitalRainSceneSelect : MonoBehaviour
{
	public bool GUIHide = false;
	
    public void LoadDigitalDemo01()
    {
        SceneManager.LoadScene("DigiRain01");
    }
    public void LoadDigitalDemo02()
    {
        SceneManager.LoadScene("DigiRain02");
    }
	public void LoadDigitalDemo03()
    {
        SceneManager.LoadScene("DigiRain03");
    }
	public void LoadDigitalDemo04()
    {
        SceneManager.LoadScene("DigiRain04");
    }
	public void LoadDigitalDemo05()
    {
        SceneManager.LoadScene("DigiRain05");
    }
	public void LoadDigitalDemo06()
    {
        SceneManager.LoadScene("DigiRain06");
    }
	public void LoadDigitalDemo07()
    {
        SceneManager.LoadScene("DigiRain07");
    }
	public void LoadDigitalDemo08()
    {
        SceneManager.LoadScene("DigiRain08");
    }
	public void LoadDigitalDemo09()
    {
        SceneManager.LoadScene("DigiRain09");
    }
	public void LoadDigitalDemo10()
    {
        SceneManager.LoadScene("DigiRain10");
    }
	public void LoadDigitalDemo11()
    {
        SceneManager.LoadScene("DigiRain11");
    }
	
	void Update ()
	{
 
     if(Input.GetKeyDown(KeyCode.H))
	 {
         GUIHide = !GUIHide;
     
         if (GUIHide)
		 {
             GameObject.Find("DigitalRainCanvas").GetComponent<Canvas> ().enabled = false;
         }
		 else
		 {
             GameObject.Find("DigitalRainCanvas").GetComponent<Canvas> ().enabled = true;
         }
     }
	}
}