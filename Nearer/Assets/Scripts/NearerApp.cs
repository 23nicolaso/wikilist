using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.UIElements;

namespace Nearer
{
    public sealed class NearerApp : MonoBehaviour
    {
        public static NearerApp Instance {get;private set;}
        public PuzzleBank Bank {get;private set;}
        public Puzzle Puzzle {get;private set;}
        public RoundState State {get;private set;}
        public bool ReducedMotion {get;private set;}
        public bool IsRevealing {get;private set;}
        public string Mode => "stream";
        public int Selected {get;private set;}=-1;
        public string StorageKey {get;private set;}
        QuestionStream stream;
        MediaBank media;
        VisualElement root,app,board,overlay,adjust,evidence,centerGroup,centerPhoto;
        VisualElement[] nodes,photos;
        Label anchor,edition,instruction,status,score;
        Button action,share,imagesButton;
        FieldDrawing field;
        PanelSettings panel;
        Texture2D wash;
        readonly Color[] washPixels=new Color[32*32];
        readonly Dictionary<string,Texture2D> textures=new Dictionary<string,Texture2D>();
        Vector2 center;
        Vector2[] current=new Vector2[5],targets=new Vector2[5],nodeSizes=new Vector2[5],ghosts=new Vector2[5];
        float radius,innerRadius;
        float PhotoOffset=>images&&!compact?(wide?15.5f:13.5f):0;
        bool compact,wide,hasRail,layoutDirty=true,dragging,moved,images=true,targetsReady,initialized;
        int dragPointer,question;
        Vector2 pointerStart,dragOffset;
        string date,notice;
        Palette fromPalette,toPalette,theme;
        float paletteTime=1;
#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")]static extern string NearerRead(string key);
        [DllImport("__Internal")]static extern int NearerWrite(string key,string value);
        [DllImport("__Internal")]static extern int NearerMotionPreference();
        [DllImport("__Internal")]static extern void NearerCopy(string value);
        [DllImport("__Internal")]static extern float NearerPixelRatio();
#endif
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Bootstrap(){if(FindFirstObjectByType<NearerApp>()==null)new GameObject("Nearer").AddComponent<NearerApp>();}
        void Awake()
        {
            Instance=this;Application.targetFrameRate=60;
            var camera=new GameObject("Camera").AddComponent<Camera>();camera.clearFlags=CameraClearFlags.SolidColor;camera.cullingMask=0;camera.backgroundColor=FieldDrawing.Mineral;
            Bank=JsonUtility.FromJson<PuzzleBank>(Resources.Load<TextAsset>("nearer-puzzles").text);
            stream=new QuestionStream(JsonUtility.FromJson<SourceCatalog>(Resources.Load<TextAsset>("nearer-catalog").text));
            var asset=Resources.Load<TextAsset>("nearer-media");
            media=asset==null?new MediaBank{entries=Array.Empty<MediaCredit>()}:JsonUtility.FromJson<MediaBank>(asset.text);
            ReducedMotion=Read("nearer.motion")=="reduced";
#if UNITY_WEBGL && !UNITY_EDITOR
            if(string.IsNullOrEmpty(Read("nearer.motion")))ReducedMotion=NearerMotionPreference()==1;
#endif
            images=Read("nearer.images")!="off";
            date=DateTime.UtcNow.ToString("yyyy-MM-dd",CultureInfo.InvariantCulture);
            int.TryParse(Read("nearer.v3.sequence."+date),out question);question=Mathf.Max(0,question);
            panel=Resources.Load<PanelSettings>("NearerPanel");
            if(panel==null)panel=ScriptableObject.CreateInstance<PanelSettings>();
            panel.scaleMode=PanelScaleMode.ConstantPixelSize;panel.scale=1;
#if UNITY_WEBGL && !UNITY_EDITOR
            panel.scale=Mathf.Max(1,NearerPixelRatio());
#endif
            panel.clearColor=true;
            var doc=gameObject.AddComponent<UIDocument>();doc.panelSettings=panel;
            root=doc.rootVisualElement;root.styleSheets.Add(Resources.Load<StyleSheet>("Nearer"));
            wash=new Texture2D(32,32,TextureFormat.RGBA32,false){name="Nearer atmosphere",filterMode=FilterMode.Bilinear,wrapMode=TextureWrapMode.Clamp};
            BuildUI();OpenQuestion(false);initialized=true;
        }
        void BuildUI()
        {
            app=Element(root,"app");app.style.backgroundImage=wash;
            var top=Element(app,"topbar");Text(top,"nearer","brand");
            var nav=Element(top,"navigation");imagesButton=Button(nav,"Images on",ToggleImages,"quiet");Button(nav,"How to play",ShowHelp,"quiet");
            edition=Text(app,"","edition");instruction=Text(app,"Bring related things closer.","instruction");score=Text(app,"","score");
            board=Element(app,"board");field=new FieldDrawing();field.StretchToParentSize();board.Add(field);
            centerGroup=Element(board,"center-group");centerPhoto=Element(centerGroup,"center-photo");anchor=Text(centerGroup,"","anchor");
            anchor.RegisterCallback<GeometryChangedEvent>(e=>{if(Vector2.Distance(e.newRect.size,e.oldRect.size)>0.5f)layoutDirty=true;});
            centerGroup.pickingMode=PickingMode.Ignore;centerPhoto.pickingMode=PickingMode.Ignore;anchor.pickingMode=PickingMode.Ignore;
            nodes=new VisualElement[5];photos=new VisualElement[5];
            for(int i=0;i<5;i++)
            {
                int index=i;var node=Element(board,"topic");node.name="topic-"+i;node.focusable=true;
                photos[i]=Element(node,"topic-photo");photos[i].pickingMode=PickingMode.Ignore;
                var label=Text(node,"","topic-label");label.pickingMode=PickingMode.Ignore;
                node.RegisterCallback<PointerDownEvent>(e=>NodeDown(index,e));node.RegisterCallback<PointerMoveEvent>(e=>NodeMove(index,e));node.RegisterCallback<PointerUpEvent>(e=>NodeUp(index,e));
                node.RegisterCallback<PointerCaptureOutEvent>(e=>{if(dragging){dragging=false;nodes[index].RemoveFromClassList("dragging");Save();}});
                node.RegisterCallback<KeyDownEvent>(e=>NodeKey(index,e));node.RegisterCallback<FocusInEvent>(e=>Select(index));nodes[i]=node;
            }
            board.RegisterCallback<GeometryChangedEvent>(e=>{if(Vector2.Distance(e.newRect.size,e.oldRect.size)>0.5f)layoutDirty=true;});
            board.RegisterCallback<PointerDownEvent>(e=>{if(e.target!=board||Selected<0||overlay!=null||State.revealed)return;Place(Selected,board.WorldToLocal(e.position));e.StopPropagation();});
            adjust=Element(app,"adjust");Button(adjust,"Closer",()=>Nudge(-0.065f),"quiet nudge");Button(adjust,"Farther",()=>Nudge(0.065f),"quiet nudge");
            var dock=Element(app,"dock");action=Button(dock,"Reveal",MainAction,"action");share=Button(dock,"Share",Share,"quiet share");status=Text(app,"","status");
            var footer=Element(app,"footer");Button(footer,"The connections",ShowSources,"quiet foot-button");
            root.RegisterCallback<GeometryChangedEvent>(e=>{if(Vector2.Distance(e.newRect.size,e.oldRect.size)<0.5f)return;bool small=e.newRect.width<700,large=e.newRect.width>1500,rail=e.newRect.width>=1280;if(compact!=small||wide!=large||hasRail!=rail){compact=small;wide=large;hasRail=rail;UpdateNodeSizes();UpdateEvidence();}app.EnableInClassList("compact",compact);app.EnableInClassList("wide",wide);app.EnableInClassList("has-rail",hasRail);layoutDirty=true;});
            root.RegisterCallback<KeyDownEvent>(e=>{if(e.keyCode==KeyCode.Escape){CloseOverlay();if(!State.revealed)Selected=-1;UpdateUI();}});
        }
        static VisualElement Element(VisualElement parent,string classes){var e=new VisualElement();foreach(string c in classes.Split(' '))e.AddToClassList(c);parent.Add(e);return e;}
        static Label Text(VisualElement parent,string value,string classes){var e=new Label(value);foreach(string c in classes.Split(' '))e.AddToClassList(c);parent.Add(e);return e;}
        Button Button(VisualElement parent,string value,Action callback,string classes){var e=new Button(callback){text=value};foreach(string c in classes.Split(' '))e.AddToClassList(c);if(e.ClassListContains("action"))e.style.backgroundColor=theme.Accent;parent.Add(e);return e;}
        void OpenQuestion(bool transition)
        {
            StopAllCoroutines();IsRevealing=false;dragging=false;Selected=-1;notice=null;CloseOverlay();
            Puzzle=stream.Generate(date,question);StorageKey="nearer.v3.round."+date+"."+question;
            RoundState saved=null;try{string json=Read(StorageKey);if(!string.IsNullOrEmpty(json))saved=JsonUtility.FromJson<RoundState>(json);}catch{}
            State=Rules.Valid(saved,Puzzle)?saved:Rules.NewRound(Puzzle);
            if(State.revealed)Selected=0;
            anchor.text=Puzzle.anchor;edition.text="Map "+(question+1).ToString("00");
            for(int i=0;i<5;i++){nodes[i].Q<Label>(className:"topic-label").text=Puzzle.topics[i].label;SetPhoto(photos[i],Puzzle.topics[i].title);}
            SetPhoto(centerPhoto,Puzzle.anchorTitle);UpdateNodeSizes();targetsReady=false;layoutDirty=true;
            fromPalette=transition?theme:Palette.At(question);toPalette=Palette.At(question);paletteTime=transition&&!ReducedMotion?0:1;
            theme=fromPalette;ApplyTheme();UpdateUI();Write("nearer.v3.sequence."+date,question.ToString(CultureInfo.InvariantCulture));
        }
        public void Next(){if(IsRevealing)return;question++;OpenQuestion(true);}
        public void OpenPractice()=>Next();
        public void OpenDaily(){question=0;OpenQuestion(true);}
        void MainAction(){if(State.revealed)Next();else Reveal();}
        void ToggleImages(){images=!images;Write("nearer.images",images?"on":"off");for(int i=0;i<5;i++)SetPhoto(photos[i],Puzzle.topics[i].title);SetPhoto(centerPhoto,Puzzle.anchorTitle);UpdateNodeSizes();UpdateUI();}
        void SetPhoto(VisualElement element,string title)
        {
            MediaCredit credit=Array.Find(media.entries,c=>c.title==title);Texture2D image=null;
            if(credit!=null&&!textures.TryGetValue(credit.resource,out image)){image=Resources.Load<Texture2D>(credit.resource);textures[credit.resource]=image;}
            element.style.backgroundImage=!images||image==null?new StyleBackground(StyleKeyword.None):new StyleBackground(image);element.EnableInClassList("no-image",image==null);
            var initial=element.Q<Label>("image-initial");if(initial==null){initial=Text(element,"","image-initial");initial.name="image-initial";initial.pickingMode=PickingMode.Ignore;}
            initial.text=title.Substring(0,1).ToUpperInvariant();initial.style.display=images&&image==null?DisplayStyle.Flex:DisplayStyle.None;
        }
        void UpdateNodeSizes()
        {
            if(Puzzle==null)return;app.EnableInClassList("no-photos",!images);
            for(int i=0;i<5;i++){float width=Mathf.Clamp(Puzzle.topics[i].label.Length*(compact?6.8f:wide?9.4f:8.6f)+18,compact?80:112,compact?132:180);float height=images&&!compact?(wide?102:88):46;nodeSizes[i]=new Vector2(width,height);nodes[i].style.width=width;nodes[i].style.height=height;}layoutDirty=true;
        }
        void Update()
        {
            if(!initialized||State==null)return;if(layoutDirty){layoutDirty=false;SolveLayout();}bool active=false;
            for(int i=0;i<5;i++)
            {
                Vector2 next=targets[i];if(!ReducedMotion&&targetsReady&&!(dragging&&Selected==i))next=Vector2.Lerp(current[i],next,1-Mathf.Exp(-Time.unscaledDeltaTime*(IsRevealing?5:17)));
                if(Vector2.Distance(current[i],next)>0.05f){current[i]=next;active=true;}else current[i]=targets[i];
                nodes[i].style.left=center.x+current[i].x-nodeSizes[i].x*0.5f;nodes[i].style.top=center.y+current[i].y-nodeSizes[i].y*0.5f+PhotoOffset;
            }
            if(paletteTime<1){paletteTime=Mathf.Min(1,paletteTime+Time.unscaledDeltaTime/1.65f);float t=paletteTime*paletteTime*(3-2*paletteTime);theme=Palette.Lerp(fromPalette,toPalette,t);ApplyTheme();active=true;}
            if(active||field.NeedsUpdate)DrawField();
        }
        void SolveLayout()
        {
            float width=board.resolvedStyle.width,height=board.resolvedStyle.height;if(float.IsNaN(width)||float.IsNaN(height)||width<10||height<10)return;
            center=new Vector2(width*0.5f,height*0.5f);radius=Mathf.Max(100,Mathf.Min(width*0.5f-(compact?70:104),height*0.5f-(compact?22:36)));
            if(hasRail)radius=Mathf.Min(radius,width*0.5f-350);
            innerRadius=compact?Mathf.Min(100,radius*0.76f):Mathf.Min(images?(wide?175:150):110,radius*0.77f);
            float centerPhotoSize=images?(compact?64:wide?108:88):0;
            centerGroup.style.left=center.x-(compact?60:100);centerGroup.style.top=center.y-(images?centerPhotoSize*0.5f:compact?11:wide?15:14);centerGroup.style.width=compact?120:200;
            Vector2[] desired=new Vector2[5];for(int i=0;i<5;i++){var p=State.positions[i];float value=State.revealed?Rules.ReferenceRadius(Puzzle,i):p.magnitude;desired[i]=p.normalized*ToPhysical(value);ghosts[i]=p.normalized*ToPhysical(p.magnitude);}
            float textWidth=anchor.resolvedStyle.width,textHeight=anchor.resolvedStyle.height;
            if(float.IsNaN(textWidth)||textWidth<1)textWidth=compact?120:200;
            if(float.IsNaN(textHeight)||textHeight<1)textHeight=compact?24:32;
            Rect captionBounds=new Rect(-textWidth*0.5f,images?centerPhotoSize*0.5f+(compact?10:15):-textHeight*0.5f,textWidth,textHeight);
            Rect anchorBounds=images?new Rect(-centerPhotoSize*0.5f,-centerPhotoSize*0.5f,centerPhotoSize,centerPhotoSize):captionBounds;
            Rect bounds=new Rect(-center.x+8,-center.y+4,width-16,height-8);
            targets=MagnetLayout.Arrange(desired,nodeSizes,bounds,anchorBounds,dragging?Selected:-1,new Vector2(0,PhotoOffset),images?(Rect?)captionBounds:null);if(!targetsReady){current=(Vector2[])targets.Clone();targetsReady=true;}field.NeedsUpdate=true;DrawField();
        }
        float ToPhysical(float normalized)=>Mathf.Lerp(innerRadius,radius,Mathf.Clamp01((normalized-Rules.MinimumRadius)/(Rules.MaximumRadius-Rules.MinimumRadius)));
        Vector2 ToNormalized(Vector2 physical){float value=Mathf.Lerp(Rules.MinimumRadius,Rules.MaximumRadius,Mathf.Clamp01((physical.magnitude-innerRadius)/Mathf.Max(1,radius-innerRadius)));return(physical.sqrMagnitude<0.001f?Vector2.up:physical.normalized)*value;}
        void DrawField(){field.Configure(center,radius,State,current,ghosts,Selected,theme.Guide,theme.Accent);}
        void NodeDown(int index,PointerDownEvent e)
        {
            if(e.button!=0||overlay!=null||IsRevealing)return;Select(index);nodes[index].Focus();e.StopPropagation();if(State.revealed)return;
            dragging=true;moved=false;dragPointer=e.pointerId;pointerStart=board.WorldToLocal(e.position);dragOffset=center+current[index]-pointerStart;nodes[index].CapturePointer(e.pointerId);nodes[index].AddToClassList("dragging");
        }
        void NodeMove(int index,PointerMoveEvent e){if(!dragging||index!=Selected||e.pointerId!=dragPointer)return;var point=board.WorldToLocal(e.position);if(Vector2.Distance(point,pointerStart)>4)moved=true;if(moved)Place(index,point+dragOffset,false);}
        void NodeUp(int index,PointerUpEvent e)
        {
            if(!dragging||index!=Selected||e.pointerId!=dragPointer)return;if(moved)Place(index,board.WorldToLocal(e.position)+dragOffset);else{notice="Choose a place for "+Puzzle.topics[index].label+".";UpdateUI();}
            dragging=false;nodes[index].RemoveFromClassList("dragging");nodes[index].ReleasePointer(e.pointerId);layoutDirty=true;e.StopPropagation();
        }
        void NodeKey(int index,KeyDownEvent e)
        {
            if(overlay!=null||IsRevealing)return;Select(index);if(State.revealed)return;var p=State.positions[index];float step=e.shiftKey?0.09f:0.035f;
            switch(e.keyCode){case KeyCode.UpArrow:p.y-=step;break;case KeyCode.DownArrow:p.y+=step;break;case KeyCode.LeftArrow:p.x-=step;break;case KeyCode.RightArrow:p.x+=step;break;case KeyCode.Return:case KeyCode.Space:break;default:return;}
            PlaceNormalized(index,p);root.focusController.IgnoreEvent(e);e.StopPropagation();
        }
        void Select(int index){if(Selected==index)return;Selected=index;notice=null;UpdateUI();}
        public void Place(int index,Vector2 localPosition,bool persist=true){if(State.revealed||IsRevealing||radius<=0)return;State.positions[index]=Rules.ClampPosition(ToNormalized(localPosition-center));State.placed[index]=true;notice=null;layoutDirty=true;if(persist)Save();UpdateUI();}
        public void PlaceNormalized(int index,Vector2 value){if(State.revealed||IsRevealing)return;State.positions[index]=Rules.ClampPosition(value);State.placed[index]=true;notice=null;layoutDirty=true;Save();UpdateUI();}
        void Nudge(float amount){if(Selected<0||State.revealed)return;var p=State.positions[Selected];PlaceNormalized(Selected,p.normalized*(p.magnitude+amount));}
        public void Reveal(){if(State.revealed||IsRevealing)return;State.revealed=true;Selected=-1;Save();IsRevealing=!ReducedMotion;layoutDirty=true;UpdateUI();StartCoroutine(FinishReveal());}
        IEnumerator FinishReveal(){if(!ReducedMotion)yield return new WaitForSecondsRealtime(1.35f);IsRevealing=false;Selected=0;UpdateUI();}
        void UpdateUI()
        {
            if(State==null)return;
            action.text=State.revealed?"Next":"Reveal";action.SetEnabled(!IsRevealing);share.style.display=State.revealed&&!IsRevealing?DisplayStyle.Flex:DisplayStyle.None;
            instruction.style.display=State.revealed?DisplayStyle.None:DisplayStyle.Flex;score.style.display=State.revealed?DisplayStyle.Flex:DisplayStyle.None;
            if(State.revealed){var s=Rules.Score(Puzzle,State);score.text=s.percent+" points     "+s.correct+" of "+s.total+" connections";}
            status.text=notice??(State.revealed?(IsRevealing?"Settling into place.":"Select a topic to explore."):"Reveal whenever you're ready.");adjust.style.display=Selected>=0&&!State.revealed?DisplayStyle.Flex:DisplayStyle.None;imagesButton.text=images?"Images on":"Images off";
            for(int i=0;i<5;i++){nodes[i].EnableInClassList("active",Selected==i);nodes[i].EnableInClassList("unplaced",!State.placed[i]);nodes[i].tooltip=Puzzle.topics[i].label;}UpdateEvidence();field.NeedsUpdate=true;
        }
        void UpdateEvidence()
        {
            evidence?.RemoveFromHierarchy();evidence=null;if(State==null||!State.revealed||IsRevealing||Selected<0)return;
            var topic=Puzzle.topics[Selected];evidence=Element(app,hasRail?"connection-panel":"evidence");
            if(hasRail)Text(evidence,topic.label,"connection-title");
            Text(evidence,topic.sharedCount+" shared links with "+Puzzle.anchor+".","connection-count");
            Text(evidence,hasRail?"Both link to":topic.label+" and "+Puzzle.anchor+" both link to","evidence-caption");var links=Element(evidence,hasRail?"connection-links":"evidence-links");
            for(int i=0;i<Mathf.Min(hasRail?4:compact?2:3,topic.evidence.Length);i++){string title=topic.evidence[i];Button(links,title,()=>OpenLink("https://en.wikipedia.org/wiki/"+Uri.EscapeDataString(title.Replace(' ','_'))),hasRail?"quiet connection-link":"quiet evidence-link");}
            if(hasRail)Button(evidence,"Read the source",()=>OpenLink(topic.url),"quiet connection-source");
        }
        void Share()
        {
            var result=Rules.Score(Puzzle,State);string text="Nearer\n"+Puzzle.anchor+"\n"+result.percent+" points\n"+result.correct+" of "+result.total+" connections";
#if UNITY_WEBGL && !UNITY_EDITOR
            NearerCopy(text);
#else
            GUIUtility.systemCopyBuffer=text;OnClipboard("copied");
#endif
        }
        public void OnClipboard(string outcome){notice=outcome=="copied"?"Result copied.":"Copy is unavailable in this browser.";UpdateUI();}
        public void SetReducedMotion(bool reduced){ReducedMotion=reduced;Write("nearer.motion",reduced?"reduced":"full");if(reduced){paletteTime=1;theme=toPalette;ApplyTheme();}}
        void ShowHelp()
        {
            var sheet=NewSheet("A sense of connection");Text(sheet,"Stronger connections belong nearer the center. Arrange as much as you like, then reveal.","sheet-copy");Text(sheet,"Only distance counts. Drag, or select a topic and choose a place. Arrow keys move it too.","sheet-copy");Text(sheet,"Each faint orbit follows a topic. Select one to highlight it. After revealing, select a topic to see its shared links.","sheet-copy");
            var motion=Button(sheet,ReducedMotion?"Motion reduced":"Motion on",null,"quiet sheet-option");motion.clicked+=()=>{SetReducedMotion(!ReducedMotion);motion.text=ReducedMotion?"Motion reduced":"Motion on";};Button(sheet,"Back",CloseOverlay,"action sheet-close");
        }
        void ShowSources()
        {
            var sheet=NewSheet("The connections");Text(sheet,"We compare the article links shared by two Wikipedia pages, adjusted for their total links. Exact pixel positions do not count. Near ties accept either order.","sheet-copy");Text(sheet,"Questions are generated from a growing source collection. The first map is shared each day. Text is credited to Wikipedia contributors under CC BY-SA 4.0.","sheet-copy");Button(sheet,"Read "+Puzzle.anchor,()=>OpenLink(Puzzle.anchorUrl),"quiet source-link");Button(sheet,"Image credits",ShowImageCredits,"quiet source-link");Button(sheet,"Text license",()=>OpenLink(Bank.licenseUrl),"quiet source-link");Button(sheet,"Back",CloseOverlay,"action sheet-close");
        }
        void ShowImageCredits()
        {
            var sheet=NewSheet("Image credits");var scroll=new ScrollView();scroll.AddToClassList("sheet-scroll");sheet.Add(scroll);var titles=new HashSet<string>{Puzzle.anchorTitle};foreach(var t in Puzzle.topics)titles.Add(t.title);
            foreach(var credit in media.entries)if(titles.Contains(credit.title)){var row=Element(scroll,"credit");Text(row,credit.title,"credit-title");Text(row,credit.author+". "+credit.license,"sheet-copy");string url=credit.url;Button(row,"Source image",()=>OpenLink(url),"quiet source-link");}Button(sheet,"Back",CloseOverlay,"action sheet-close");
        }
        VisualElement NewSheet(string title)
        {
            CloseOverlay();overlay=Element(app,"overlay");overlay.style.backgroundColor=theme.Base;var sheet=Element(overlay,"sheet");Text(sheet,title,"sheet-title");
            overlay.RegisterCallback<KeyDownEvent>(e=>{if(e.keyCode!=KeyCode.Tab)return;var buttons=overlay.Query<Button>().ToList();if(buttons.Count==0)return;int currentIndex=buttons.FindIndex(b=>b==root.focusController.focusedElement);buttons[(currentIndex+(e.shiftKey?-1:1)+buttons.Count)%buttons.Count].Focus();root.focusController.IgnoreEvent(e);e.StopImmediatePropagation();});overlay.schedule.Execute(()=>overlay?.Query<Button>().First()?.Focus());return sheet;
        }
        void CloseOverlay(){overlay?.RemoveFromHierarchy();overlay=null;}
        static void OpenLink(string url){if(url.StartsWith("https://en.wikipedia.org/",StringComparison.Ordinal)||url.StartsWith("https://commons.wikimedia.org/",StringComparison.Ordinal)||url.StartsWith("https://creativecommons.org/",StringComparison.Ordinal))Application.OpenURL(url);}
        void ApplyTheme()
        {
            root.style.color=theme.Ink;panel.colorClearValue=theme.Base;
            for(int y=0;y<32;y++)for(int x=0;x<32;x++){float u=x/31f,v=y/31f;float t=Mathf.Clamp01(u*0.55f+v*0.42f);washPixels[y*32+x]=Color.Lerp(theme.Base,theme.Second,t);}
            wash.SetPixels(washPixels);wash.Apply(false);action.style.backgroundColor=theme.Accent;
            for(int i=0;i<5;i++){photos[i].style.borderTopColor=theme.Accent;photos[i].style.borderBottomColor=theme.Accent;photos[i].style.borderLeftColor=theme.Accent;photos[i].style.borderRightColor=theme.Accent;if(photos[i].ClassListContains("no-image"))photos[i].style.backgroundColor=Color.Lerp(theme.Base,theme.Accent,0.2f);}field.NeedsUpdate=true;
        }
        struct Palette
        {
            public Color Base,Second,Accent,Ink,Guide;
            public static Palette At(int i)
            {
                string[,] colors={{"#E5EEF1","#F2EFE8","#386378"},{"#ECE8F1","#E7EFF0","#69558A"},{"#F2E8E5","#F0E9F1","#8B5263"},{"#E8EEE3","#E4EEF0","#476C57"},{"#E5EBF4","#F0ECE9","#49658B"}};i=((i%5)+5)%5;ColorUtility.TryParseHtmlString(colors[i,0],out Color a);ColorUtility.TryParseHtmlString(colors[i,1],out Color b);ColorUtility.TryParseHtmlString(colors[i,2],out Color c);return new Palette{Base=a,Second=b,Accent=c,Ink=new Color32(37,47,56,255),Guide=Color.Lerp(a,c,0.18f)};
            }
            public static Palette Lerp(Palette a,Palette b,float t)=>new Palette{Base=Color.Lerp(a.Base,b.Base,t),Second=Color.Lerp(a.Second,b.Second,t),Accent=Color.Lerp(a.Accent,b.Accent,t),Ink=Color.Lerp(a.Ink,b.Ink,t),Guide=Color.Lerp(a.Guide,b.Guide,t)};
        }
        string Read(string key){try{
#if UNITY_WEBGL && !UNITY_EDITOR
            return NearerRead(key);
#else
            return PlayerPrefs.GetString(key,"");
#endif
        }catch{return "";}}
        void Write(string key,string value){try{
#if UNITY_WEBGL && !UNITY_EDITOR
            if(NearerWrite(key,value)==0)notice="Progress stays here while this tab is open.";
#else
            PlayerPrefs.SetString(key,value);PlayerPrefs.Save();
#endif
        }catch{notice="Progress stays here while this tab is open.";}}
        void Save(){Write(StorageKey,JsonUtility.ToJson(State));}
        public string Snapshot()
        {
            var s=Rules.Score(Puzzle,State);int overlaps=0;for(int i=0;i<5;i++)for(int j=i+1;j<5;j++)if(MagnetLayout.Overlap(new Rect(targets[i]-nodeSizes[i]*0.5f,nodeSizes[i]),new Rect(targets[j]-nodeSizes[j]*0.5f,nodeSizes[j]))>1)overlaps++;
            return JsonUtility.ToJson(new RuntimeSnapshot{puzzle=Puzzle.id,anchor=Puzzle.anchor,question=question,revealed=State.revealed,animating=IsRevealing,reducedMotion=ReducedMotion,score=s.percent,correct=s.correct,total=s.total,overlaps=overlaps,width=root.resolvedStyle.width,height=root.resolvedStyle.height,state=State});
        }
        [Serializable]class RuntimeSnapshot{public string puzzle,anchor;public int question,score,correct,total,overlaps;public float width,height;public bool revealed,animating,reducedMotion;public RoundState state;}
        void OnDestroy(){if(wash!=null)Destroy(wash);if(Instance==this)Instance=null;}
    }
}
