using System;
using System.IO;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Apex {
public sealed class ReplayMenu:MonoBehaviour {
    public Button open,yes,no,back,primary,opponent,rowTemplate;
    public GameObject dialog,prompt;
    public RectTransform rows;
    public InputField nameField;
    public Text heading,hint,primaryText,opponentText;
    RaceSession session;
    SessionPanel panel;
    GhostShelf shelf;
    GhostHeader pending;
    CanvasGroup behind;
    int selected;
    bool use,confirm;
    Button[] buttons=Array.Empty<Button>();
    public void Connect(RaceSession race,SessionPanel ui){
        session=race;panel=ui;
        open.onClick.AddListener(race.OpenReplays);
        yes.onClick.AddListener(race.ChooseSave);no.onClick.AddListener(race.SkipSave);
        back.onClick.AddListener(()=>{Hide();Focus(pending!=null?yes:open);});
        opponent.onClick.AddListener(ChangeOpponent);primary.onClick.AddListener(Submit);
        nameField.onValueChanged.AddListener(_=>{confirm=false;Refresh();});
    }
    static void Focus(Button b){if(EventSystem.current && b.isActiveAndEnabled)EventSystem.current.SetSelectedGameObject(b.gameObject);}
    public void Garage(bool available){
        open.gameObject.SetActive(available);
        panel.garageCard.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical,available?448:400);
    }
    public void Offer(){
        prompt.SetActive(true);panel.retry.gameObject.SetActive(false);panel.returnFromResults.gameObject.SetActive(false);Focus(yes);
    }
    public void Decided(){
        Hide();prompt.SetActive(false);panel.retry.gameObject.SetActive(true);panel.returnFromResults.gameObject.SetActive(true);
        Focus(panel.retry);
    }
    public void Hide(){
        dialog.SetActive(false);
        if(behind){behind.interactable=true;behind=null;}
    }
    public void Library(GhostShelf archive){
        pending=null;Open(archive,0);
    }
    public void SaveDialog(GhostShelf archive,GhostHeader run){
        pending=run;use=false;
        int initial=archive.At(0)==null?0:1;
        if(initial!=0)for(int i=1;i<archive.Count;i++)if(archive.At(i)==null){initial=i;break;}
        nameField.SetTextWithoutNotify(run.title);
        Open(archive,initial);
    }
    void Open(GhostShelf archive,int initial){
        shelf=archive;selected=initial;confirm=false;
        var screen=pending!=null?panel.results:panel.garage;
        behind=screen.GetComponent<CanvasGroup>();
        if(!behind)behind=screen.AddComponent<CanvasGroup>();
        behind.interactable=false;
        if(buttons.Length!=shelf.Count){
            foreach(var button in buttons)Destroy(button.gameObject);
            buttons=new Button[shelf.Count];
            for(int i=0;i<buttons.Length;i++){
                int slot=i;var b=Instantiate(rowTemplate,rows);buttons[i]=b;b.gameObject.SetActive(true);
                b.onClick.AddListener(()=>Choose(slot));
            }
        }
        dialog.SetActive(true);Refresh();Focus(buttons[selected]);
    }
    public void Choose(int slot){if(!buttons[slot].interactable)return;selected=slot;confirm=false;Refresh();}
    string Label(int i)=>i==0?"最快专属":"槽位 "+i;
    void Refresh(){
        if(!dialog.activeSelf || shelf==null)return;
        bool saving=pending!=null;
        var current=shelf.At(selected);
        for(int i=0;i<buttons.Length;i++){
            var item=shelf.At(i);var b=buttons[i];
            b.interactable=saving?(shelf.At(0)==null?i==0:i>0):item!=null;
            string detail=item==null?"空":item.Title+"    "+SessionPanel.Clock(item.info.Duration)+(item==shelf.Active?"　参赛中":"");
            b.GetComponentInChildren<Text>().text=Label(i)+"　　"+detail;
            b.targetGraphic.color=i==selected?new Color32(43,102,86,255):new Color32(36,50,59,255);
        }
        heading.text=saving?"保存本场幽灵":"参赛幽灵";
        nameField.gameObject.SetActive(saving);
        primary.interactable=saving?!string.IsNullOrWhiteSpace(nameField.text):current!=null;
        primaryText.text=saving?(confirm?"确认替换":"保存"):(current==shelf.Active?"取消参赛":"设为参赛幽灵");
        opponent.gameObject.SetActive(saving);
        opponentText.text=use?"同时设为参赛幽灵　是":"同时设为参赛幽灵　否";
        ((RectTransform)heading.transform.parent).SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical,saving?496:406);
        if(confirm){
            hint.text="确认"+(current==null?"保存此记录":"覆盖「"+current.Title+"」")+
                (use && shelf.Active!=null?"，并更换参赛幽灵「"+shelf.Active.Title+"」":"")+"？";
        }else if(saving){
            hint.text=shelf.MovesBest(pending)?"刷新最快。本场进入最快槽，原最快移入所选槽位。":
                shelf.At(0)==null?"首条记录将进入最快专属槽。":"选择保存槽位，并输入回放名称。";
        }else hint.text=shelf.Active==null?"当前未启用参赛幽灵。":"当前参赛　"+shelf.Active.Title;
    }
    void ChangeOpponent(){
        use=!use;confirm=false;Refresh();
    }
    void Submit(){
        if(pending==null){
            try{session.UseReplay(shelf.At(selected)==shelf.Active?-1:selected);Hide();Focus(open);}
            catch(Exception e) when(e is IOException || e is UnauthorizedAccessException){hint.text="未能更新参赛幽灵　"+e.Message;}
            return;
        }
        var target=shelf.At(selected);
        if(target!=null && target==shelf.Active && !use){
            hint.text="该槽位正在参赛。请换一个槽位，或选择同时更换参赛幽灵。";return;
        }
        if(!confirm && (target!=null || use && shelf.Active!=null)){confirm=true;Refresh();return;}
        try{session.SaveReplay(selected,nameField.text,use);}
        catch(Exception e) when(e is IOException || e is UnauthorizedAccessException || e is ArgumentException || e is InvalidOperationException){
            confirm=false;Refresh();hint.text="未能保存　"+e.Message;
        }
    }
}
}
