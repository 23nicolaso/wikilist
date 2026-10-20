using System;
using System.Collections.Generic;
using UnityEngine;

namespace Nearer
{
    [Serializable] public class SourceCatalog {public int schema; public string version; public SourceArticle[] articles;}
    [Serializable] public class SourceArticle {public string title, label, url; public long revision; public string[] links, collections;}
    [Serializable] public class MediaBank {public MediaCredit[] entries;}
    [Serializable] public class MediaCredit {public string title, resource, author, license, licenseUrl, url, file, browserImage;}

    public sealed class QuestionStream
    {
        readonly SourceCatalog catalog;
        readonly HashSet<string>[] links;
        readonly float[,] overlap;
        readonly List<int>[] related, surprises;
        readonly List<int> anchors = new List<int>();
        readonly List<List<int>> anchorGroups = new List<List<int>>();
        readonly List<string> groupNames = new List<string>();
        readonly HashSet<string> familiar;
        readonly Dictionary<string,int> articleIndex=new Dictionary<string,int>(StringComparer.Ordinal);
        readonly Dictionary<string,string[]> paths=new Dictionary<string,string[]>(StringComparer.Ordinal);
        static readonly HashSet<string> Generic = new HashSet<string> {"United States", "United Kingdom", "France", "Germany", "Europe", "China", "India", "Italy", "Spain", "Japan", "Canada", "Australia", "Brazil", "US$", "World War II", "World War I", "English language", "Latin", "The New York Times", "The Guardian", "BBC News"};

        public QuestionStream(SourceCatalog source)
        {
            catalog = source;
            int n = source.articles.Length;
            links = new HashSet<string>[n];
            related = new List<int>[n]; surprises = new List<int>[n];
            overlap = new float[n,n]; familiar = new HashSet<string>();
            for (int i=0;i<n;i++) {links[i] = new HashSet<string>(source.articles[i].links);familiar.Add(source.articles[i].title);articleIndex[source.articles[i].title]=i;}
            for (int i=0;i<n;i++)
            {
                related[i] = new List<int>(); surprises[i] = new List<int>();
                for (int j=0;j<n;j++)
                {
                    if (i == j) continue;
                    var shared = Intersection(i,j);
                    overlap[i,j] = (float)shared.Count / (links[i].Count + links[j].Count - shared.Count);
                    int useful=0;foreach(string title in shared)if(!Generic.Contains(title))useful++;
                    if (useful < 3 || overlap[i,j] < 0.002f) continue;
                    if(Related(source.articles[i],source.articles[j])) related[i].Add(j);
                    else if(useful>=8) surprises[i].Add(j);
                }
                int a = i;
                related[i].Sort((x,y)=>overlap[a,y].CompareTo(overlap[a,x]));
                surprises[i].Sort((x,y)=>overlap[a,y].CompareTo(overlap[a,x]));
                // A center needs several meaningful choices, rather than unrelated filler.
                if (related[i].Count >= 7) anchors.Add(i);
            }
            foreach(int index in anchors)
            {
                string group=Primary(source.articles[index]);int g=groupNames.IndexOf(group);
                if(g<0){g=groupNames.Count;groupNames.Add(group);anchorGroups.Add(new List<int>());}
                anchorGroups[g].Add(index);
            }
            if (anchors.Count < 7) throw new InvalidOperationException("The source catalog needs more connected topics.");
        }

        static string Primary(SourceArticle article)=>article.collections!=null&&article.collections.Length>0?article.collections[0]:"general";
        public static bool Related(SourceArticle a,SourceArticle b)
        {
            if(a.collections==null||b.collections==null)return Primary(a)==Primary(b);
            foreach(string group in a.collections)if(Array.IndexOf(b.collections,group)>=0)return true;
            return false;
        }
        static void Shuffle(List<int> values,System.Random random){for(int i=values.Count-1;i>0;i--){int j=random.Next(i+1);int old=values[i];values[i]=values[j];values[j]=old;}}
        public int AnchorCount => anchors.Count;
        public int ArticleCount => catalog.articles.Length;
        public int CollectionCount => anchorGroups.Count;

        public string[] ShortestPath(string from,string to)
        {
            string key=from+"\n"+to;
            if(paths.TryGetValue(key,out var cached))return cached;
            var result=FindShortestPath(catalog,from,to);
            paths[key]=result;return result;
        }

        // Breadth-first search follows outgoing links only. Missing article bodies
        // cannot be expanded, so a result is shortest within the frozen collection.
        public static string[] FindShortestPath(SourceCatalog source,string from,string to)
        {
            var articles=new Dictionary<string,SourceArticle>(StringComparer.Ordinal);
            foreach(var article in source.articles)articles[article.title]=article;
            var previous=new Dictionary<string,string>(StringComparer.Ordinal){{from,null}};
            var queue=new Queue<string>();queue.Enqueue(from);
            while(queue.Count>0)
            {
                string title=queue.Dequeue();
                if(title==to)
                {
                    var route=new List<string>();
                    for(string node=to;node!=null;node=previous[node])route.Add(node);
                    route.Reverse();return route.ToArray();
                }
                if(!articles.TryGetValue(title,out var article))continue;
                foreach(string target in article.links)
                {
                    if(previous.ContainsKey(target)||target!=to&&!articles.ContainsKey(target))continue;
                    previous[target]=title;queue.Enqueue(target);
                }
            }
            return Array.Empty<string>();
        }

        public bool HasLink(string from,string to)=>articleIndex.TryGetValue(from,out int index)&&links[index].Contains(to);
        public string ArticleUrl(string title)=>articleIndex.TryGetValue(title,out int index)?catalog.articles[index].url:"https://en.wikipedia.org/wiki/"+Uri.EscapeDataString(title.Replace(' ','_'));

        public Puzzle Generate(string day, int number)
        {
            uint seed = Hash(day + ":" + number + ":" + catalog.version + ":3");
            var random = new System.Random((int)(seed & 0x7fffffff));
            // Every block visits each collection once. Centers rotate within each collection.
            int cycle=number/anchorGroups.Count;
            var groups=new List<int>();for(int i=0;i<anchorGroups.Count;i++)groups.Add(i);
            Shuffle(groups,new System.Random((int)(Hash(day+":collections:"+cycle)&0x7fffffff)));
            int group=groups[number%groups.Count];
            var deck=new List<int>(anchorGroups[group]);
            Shuffle(deck,new System.Random((int)(Hash(day+":"+groupNames[group]+":"+cycle/deck.Count)&0x7fffffff)));
            int anchor=deck[cycle%deck.Count];
            var pool=related[anchor];
            var unexpected=new List<int>();
            float threshold=overlap[anchor,pool[pool.Count/2]];
            foreach(int index in surprises[anchor])if(overlap[anchor,index]>=threshold)unexpected.Add(index);
            for (int attempt=0;attempt<400;attempt++)
            {
                var chosen = new HashSet<int>();
                chosen.Add(pool[random.Next(Math.Min(3,pool.Count))]);
                chosen.Add(pool[random.Next(Math.Min(3,pool.Count-1),Math.Min(8,pool.Count))]);
                bool wildcard=number%4==3&&unexpected.Count>0;
                chosen.Add(wildcard?unexpected[random.Next(Math.Min(3,unexpected.Count))]:pool[random.Next(pool.Count/2,pool.Count)]);
                int guard=0;
                while(chosen.Count<5 && guard++<100) chosen.Add(pool[random.Next(Math.Min(pool.Count,16))]);
                if(chosen.Count!=5)continue;
                var topics = new List<Topic>();
                foreach(int index in chosen)
                {
                    var article = catalog.articles[index];
                    var shared = Intersection(anchor,index);
                    var evidence = new List<string>();
                    foreach(string name in shared)if(familiar.Contains(name))evidence.Add(name);
                    foreach(string name in shared) if(!evidence.Contains(name)&&!Generic.Contains(name) && name.Length<60 && name.Length>2 && !char.IsDigit(name[0]) && !name.Contains("/")) evidence.Add(name);
                    if(evidence.Count<3) foreach(string name in shared) if(!evidence.Contains(name)) evidence.Add(name);
                    if(evidence.Count>8)evidence.RemoveRange(8,evidence.Count-8);
                    topics.Add(new Topic {title=article.title,label=article.label,revision=article.revision,url=article.url,linkCount=links[index].Count,sharedCount=shared.Count,unionCount=links[anchor].Count+links[index].Count-shared.Count,similarity=overlap[anchor,index],evidence=evidence.ToArray()});
                }
                topics.Sort((x,y)=>y.similarity.CompareTo(x.similarity));
                float tolerance=topics[0].similarity*0.08f;
                int informative=0;
                for(int i=0;i<5;i++) for(int j=i+1;j<5;j++) if(Mathf.Abs(topics[i].similarity-topics[j].similarity)>tolerance)informative++;
                if(informative<6 || topics[0].similarity < topics[4].similarity*1.5f)continue;
                var center = catalog.articles[anchor];
                return new Puzzle {id="stream-"+seed.ToString("x8"),day=number,category=groupNames[group],anchor=center.label,anchorTitle=center.title,anchorRevision=center.revision,anchorUrl=center.url,anchorLinkCount=links[anchor].Count,tieTolerance=tolerance,informativePairs=informative,topics=topics.ToArray()};
            }
            throw new InvalidOperationException("A varied question could not be generated for "+catalog.articles[anchor].title);
        }

        List<string> Intersection(int a,int b)
        {
            var result = new List<string>();
            foreach(string name in catalog.articles[a].links) if(links[b].Contains(name)) result.Add(name);
            return result;
        }

        public static uint Hash(string value) {uint n=2166136261;foreach(char c in value)n=unchecked((n^c)*16777619);return n;}
    }

    public static class MagnetLayout
    {
        public static Vector2[] Arrange(Vector2[] desired, Vector2[] sizes, Rect bounds, Rect anchor, int selected,Vector2 labelOffset=default,Rect? caption=null)
        {
            var answer=new Vector2[desired.Length];
            var occupied=new List<Rect> {Expand(anchor,9)};
            if(caption.HasValue)occupied.Add(Expand(caption.Value,5));
            var order=new List<int>();
            if(selected>=0)order.Add(selected);
            for(int i=0;i<desired.Length;i++)if(i!=selected)order.Add(i);
            foreach(int index in order)
            {
                var d=desired[index];float radius=d.magnitude;float angle=Mathf.Atan2(d.y,d.x);
                float best=float.MaxValue;Vector2 chosen=d;
                for(int step=0;step<144;step++)
                {
                    int offset=step==0?0:(step+1)/2*(step%2==1?1:-1);
                    float a=angle+offset*5*Mathf.Deg2Rad;
                    var point=new Vector2(Mathf.Cos(a),Mathf.Sin(a))*radius;
                    var rect=new Rect(point+labelOffset-sizes[index]*0.5f,sizes[index]);
                    float penalty=0;
                    foreach(var other in occupied)penalty+=Overlap(rect,other)*10000;
                    penalty+=(Mathf.Max(bounds.xMin-rect.xMin,0)+Mathf.Max(rect.xMax-bounds.xMax,0)+Mathf.Max(bounds.yMin-rect.yMin,0)+Mathf.Max(rect.yMax-bounds.yMax,0))*100000;
                    penalty+=Mathf.Abs(offset);
                    if(penalty<best){best=penalty;chosen=point;}
                    if(penalty<0.01f)break;
                }
                answer[index]=chosen;
                occupied.Add(Expand(new Rect(chosen+labelOffset-sizes[index]*0.5f,sizes[index]),7));
            }
            return answer;
        }
        public static Rect Expand(Rect r,float amount)=>new Rect(r.x-amount,r.y-amount,r.width+amount*2,r.height+amount*2);
        public static float Overlap(Rect a,Rect b)=>Mathf.Max(0,Mathf.Min(a.xMax,b.xMax)-Mathf.Max(a.xMin,b.xMin))*Mathf.Max(0,Mathf.Min(a.yMax,b.yMax)-Mathf.Max(a.yMin,b.yMin));
    }
}
