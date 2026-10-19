using UnityEngine;
using UnityEngine.UIElements;
namespace Nearer
{
    public sealed class FieldDrawing:VisualElement
    {
        public static readonly Color Mineral=new Color32(229,238,241,255);
        public bool NeedsUpdate=true;
        readonly VisualElement reference;
        readonly VisualElement[] guides=new VisualElement[5],originals=new VisualElement[5],corrections=new VisualElement[5];
        readonly Label near,far;
        readonly VisualElement[] ticks=new VisualElement[255];
        readonly Label[] numbers=new Label[55];
        readonly VisualElement[,] dots=new VisualElement[5,25];
        public FieldDrawing()
        {
            pickingMode=PickingMode.Ignore;reference=Line();near=Caption("STRONGER / NEARER");far=Caption("WEAKER / FARTHER");
            for(int tick=0;tick<255;tick++)ticks[tick]=Line();
            for(int n=0;n<55;n++)numbers[n]=Caption(((n%11)*10).ToString());
            for(int i=0;i<5;i++)
            {
                guides[i]=Line();originals[i]=Line();corrections[i]=Line();
                originals[i].style.borderTopWidth=originals[i].style.borderRightWidth=originals[i].style.borderBottomWidth=originals[i].style.borderLeftWidth=2;
                originals[i].style.borderTopLeftRadius=originals[i].style.borderTopRightRadius=originals[i].style.borderBottomLeftRadius=originals[i].style.borderBottomRightRadius=30;
                for(int dot=0;dot<25;dot++)dots[i,dot]=Line();
            }
        }
        VisualElement Line(){var line=new VisualElement{pickingMode=PickingMode.Ignore};line.style.position=Position.Absolute;Add(line);return line;}
        Label Caption(string text){var label=new Label(text){pickingMode=PickingMode.Ignore};label.style.position=Position.Absolute;label.style.fontSize=10;label.style.color=(Color)new Color32(99,116,128,255);Add(label);return label;}
        public void Configure(Vector2 origin,float size,RoundState round,Vector2[] display,Vector2[] original,int selection,Color guide,Color accent,bool correcting,float minimum=0,bool[] awarded=null)
        {
            reference.style.display=near.style.display=far.style.display=DisplayStyle.None;
            Color green=new Color(0.204f,0.525f,0.365f,1);
            for(int i=0;i<5;i++)
            {
                float y=origin.y+display[i].y,axisY=y+52;
                Set(guides[i],origin.x+minimum,axisY,size-minimum,1,new Color(accent.r,accent.g,accent.b,0.4f));
                for(int tick=0;tick<=50;tick++)
                {
                    float x=origin.x+Mathf.Lerp(minimum,size,tick/50f);Set(ticks[i*51+tick],x,axisY,1,tick%5==0?11:5,accent);
                    if(tick%5==0){var label=numbers[i*11+tick/5];label.style.left=x-8;label.style.top=axisY+13;}
                }corrections[i].style.display=DisplayStyle.None;
                bool show=round.revealed&&(awarded==null?correcting:awarded[i]);originals[i].style.display=show?DisplayStyle.Flex:DisplayStyle.None;
                originals[i].style.opacity=1;
                originals[i].style.borderTopColor=originals[i].style.borderRightColor=originals[i].style.borderBottomColor=originals[i].style.borderLeftColor=green;
                Set(originals[i],origin.x+original[i].x-30,y-30,60,60,Color.clear);
                for(int dot=0;dot<25;dot++)
                {
                    var mark=dots[i,dot];mark.style.display=show&&Mathf.Abs(original[i].x-display[i].x)>1?DisplayStyle.Flex:DisplayStyle.None;
                    float t=dot/24f;float x=origin.x+Mathf.Lerp(display[i].x,original[i].x,t);float dy=y+30+4*t*(1-t)*16;
                    Set(mark,x,dy,3,3,green);mark.style.opacity=1;
                }
            }
            NeedsUpdate=false;
        }
        static void Set(VisualElement element,float x,float y,float width,float height,Color color){element.style.left=x;element.style.top=y;element.style.width=width;element.style.height=height;element.style.backgroundColor=color;}
    }
}
