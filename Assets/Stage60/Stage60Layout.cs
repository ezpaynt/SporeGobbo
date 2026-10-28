using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using SporeGobbo.Geometry;
using SporeGobbo.CampCandidate;
using SporeGobbo.CampLifecycle;

public static class Stage60Layout
{
    static readonly System.Runtime.CompilerServices.ConditionalWeakTable<AuthoredCamp,CampResidentialCatalog> catalogs=new System.Runtime.CompilerServices.ConditionalWeakTable<AuthoredCamp,CampResidentialCatalog>();
    public const string RootId="stage60.root.first-dorm";
    public static readonly Point2 RootFront=new Point2(105.5,100.5);
    public static readonly Point2 RootCenter=new Point2(105.5,95.5);
    public static List<DualTile> RootCells(){var cells=new List<DualTile>();for(int y=94;y<=96;y++)for(int x=103;x<=107;x++)cells.Add(new DualTile(x,y));return cells;}
    public static AuthoredCamp Create()
    {
        var original=Stage57Layout.Create();var cells=new CampCell[192*192];
        for(int y=0;y<192;y++)for(int x=0;x<192;x++)cells[y*192+x]=original.Category(x,y);
        var publicSpine=new HashSet<DualTile>();var domain=new HashSet<DualTile>();foreach(var room in original.Bedrooms)foreach(var p in room.Cells)domain.Add(p);
        void Tube(int ax,int ay,int bx,int by,int radius)
        {int n=Math.Max(Math.Abs(bx-ax),Math.Abs(by-ay))*2;for(int i=0;i<=n;i++){double t=i/(double)Math.Max(1,n);int cx=(int)Math.Round(ax+(bx-ax)*t),cy=(int)Math.Round(ay+(by-ay)*t);for(int y=cy-radius;y<=cy+radius;y++)for(int x=cx-radius;x<=cx+radius;x++)if((x-cx)*(x-cx)+(y-cy)*(y-cy)<=radius*radius){var p=new DualTile(x,y);domain.Add(p);publicSpine.Add(p);}}}
        Tube(105,92,112,92,3);Tube(112,92,114,89,3);
        for(int y=91;y<=93;y++)for(int x=103;x<=107;x++)domain.Add(new DualTile(x,y));Tube(114,89,115,76,3);Tube(105,75,117,75,3);
        // Thin authored structural collar; room centers, beds and camera stay unchanged.
        foreach(var p in domain)for(int y=p.Y-2;y<=p.Y+2;y++)for(int x=p.X-2;x<=p.X+2;x++)
            if(!domain.Contains(new DualTile(x,y)))cells[y*192+x]=CampCell.Permanent;
        foreach(var p in domain)
        {
            bool roomCell=false;foreach(var r in original.Bedrooms)if(r.Cells.Contains(p)){roomCell=true;break;}
            cells[p.Y*192+p.X]=!publicSpine.Contains(p)&&roomCell&&original.Category(p.X,p.Y)==CampCell.Residential?CampCell.Residential:CampCell.Open;
        }
        // The old low spine cannot remain a second entrance. Close only its lower domain, not central Camp earth.
        foreach(var p in domain)if(p.Y>=94)cells[p.Y*192+p.X]=CampCell.Permanent;
        for(int y=91;y<=93;y++)for(int x=103;x<=107;x++)if(cells[y*192+x]==CampCell.Permanent)cells[y*192+x]=CampCell.Residential;
        // Single five-cell mouth; all other public/diggable bypasses meet the collar.
        for(int y=92;y<=101;y++)for(int x=103;x<=107;x++)cells[y*192+x]=y>=94&&y<=96?CampCell.FirstBuddy:y>=97?CampCell.Open:cells[y*192+x];
        var markers=new List<CampMarker>(original.Markers);
        markers.Add(new CampMarker(RootId,"Ancient roots",new Point2(102.5,95.5),RootFront,CampCell.FirstBuddy,"terrain.root"));
        return new AuthoredCamp(cells,markers,new List<CampBedroom>(original.Bedrooms),"camp.stage60.first-buddy.v1");
    }
    // Catalog reuses existing bed identities, shared entrance and three-cell construction spines.
    public static CampResidentialCatalog Catalog(AuthoredCamp definition)=>catalogs.GetValue(definition,BuildCatalog);
    static CampResidentialCatalog BuildCatalog(AuthoredCamp definition)
    {
        var rooms=new List<CampResidentialRoomDefinition>();int progression=0;
        foreach(var room in definition.Bedrooms)
        {
            progression++;var slots=new List<CampResidentialSlotDefinition>();
            for(int j=0;j<room.Beds.Count;j++)
            {
                int slot=room.FirstSlot+j;var bed=room.Beds[j];
                var spine=PotentialSpine(definition,room,((int)room.Entrance.X,(int)room.Entrance.Y),((int)bed.X,(int)bed.Y));
                var owned=new HashSet<(int x,int y)>();
                foreach(var p in spine)for(int oy=-1;oy<=1;oy++)for(int ox=-1;ox<=1;ox++)if(definition.Category(p.x+ox,p.y+oy)==CampCell.Residential)owned.Add((p.x+ox,p.y+oy));
                slots.Add(new CampResidentialSlotDefinition(slot,slot-1,((int)bed.X,(int)bed.Y),((int)bed.X,(int)bed.Y),((int)room.Entrance.X,(int)room.Entrance.Y),spine,owned.OrderBy(p=>p.y).ThenBy(p=>p.x).ToArray(),new[]{((int)room.Entrance.X,(int)room.Entrance.Y)}));
            }
            rooms.Add(new CampResidentialRoomDefinition(room.Id,room.Id,progression,new CampCellRect(90,54,50,45),new CampCellRect((int)room.Entrance.X-1,(int)room.Entrance.Y-1,3,3),slots));
        }
        return new CampResidentialCatalog(rooms);
    }
    static List<(int x,int y)> PotentialSpine(AuthoredCamp definition,CampBedroom room,(int x,int y) start,(int x,int y) goal)
    {
        var owned=new HashSet<DualTile>(room.Cells);
        bool Allowed(int x,int y){var kind=definition.Category(x,y);return kind==CampCell.Open||kind==CampCell.FirstBuddy||kind==CampCell.Residential&&(owned.Contains(new DualTile(x,y))||room.FirstSlot==1&&x>=103&&x<=107&&y>=91&&y<=93);}
        bool Fits(int x,int y){for(int dy=-1;dy<=1;dy++)for(int dx=-1;dx<=1;dx++)if(!Allowed(x+dx,y+dy))return false;return true;}
        int a=start.y*192+start.x,b=goal.y*192+goal.x;var prior=new int[192*192];for(int i=0;i<prior.Length;i++)prior[i]=-2;var queue=new Queue<int>();queue.Enqueue(a);prior[a]=-1;
        while(queue.Count>0){int i=queue.Dequeue();if(i==b)break;int x=i%192,y=i/192;foreach(int n in new[]{i-1,i+1,i-192,i+192}){int nx=n%192,ny=n/192;if(nx<0||ny<0||nx>=192||ny>=192||Math.Abs(nx-x)+Math.Abs(ny-y)!=1||prior[n]!=-2||!Fits(nx,ny))continue;prior[n]=i;queue.Enqueue(n);}}
        if(prior[b]==-2)throw new InvalidOperationException("No authored three-cell construction spine: "+room.Id+" / "+goal);
        var path=new List<(int x,int y)>();for(int i=b;i>=0;i=prior[i])path.Add((i%192,i/192));path.Reverse();return path;
    }
    public static bool PotentialBypass(AuthoredCamp definition)
    {
        var visited=new HashSet<DualTile>();var queue=new Queue<DualTile>();var start=new DualTile(84,117);queue.Enqueue(start);visited.Add(start);
        while(queue.Count>0){var p=queue.Dequeue();foreach(var bed in definition.Bedrooms[0].Beds)if(p.X==(int)bed.X&&p.Y==(int)bed.Y)return true;
            foreach(var d in new[]{new DualTile(-1,0),new DualTile(1,0),new DualTile(0,-1),new DualTile(0,1)}){var n=new DualTile(p.X+d.X,p.Y+d.Y);var k=definition.Category(n.X,n.Y);if(k!=CampCell.Permanent&&k!=CampCell.FirstBuddy&&visited.Add(n))queue.Enqueue(n);}}
        return false;
    }
}

public sealed class Stage60ResidenceTerrain:IResidentialPlanTerrain
{
    public readonly Stage59Authority Authority;public readonly CampResidentialCatalog Catalog;
    public Stage60ResidenceTerrain(Stage59Authority authority){Authority=authority;Catalog=Stage60Layout.Catalog(authority.Session.Definition);}
    public float CellSize=>Authority.CellSize;
    public CampResidentialCatalog GetResidentialCatalog()=>Catalog;
    public List<Vector2Int> GetResidentialSlotFootprint(int slot)=>Catalog.GetSlot(slot).ExcavationFootprint.Select(p=>new Vector2Int(p.x,p.y)).ToList();
    public List<Vector2Int> GetResidentialConstructionRoute(int slot)=>Catalog.GetSlot(slot).ConstructionRoute.Select(p=>new Vector2Int(p.x,p.y)).ToList();
    public CampDigCategory GetSpatialDigCategory(Vector2Int c)=>Authority.Session.Definition.Category(c.x,c.y)==CampCell.Residential?CampDigCategory.ResidentialReserved:CampDigCategory.NormalCampDiggable;
    public bool IsBlocked(Vector2Int c)=>Authority.IsBlocked(c);
    public Vector2 CellToWorld(Vector2Int c)=>Authority.CellToWorld(c);
    public Vector2Int WorldToCell(Vector2 p)=>Authority.WorldToCell(p);
    public bool IsDerivedTerrainCollider(Collider2D c)=>Authority.CollisionRoot!=null&&c.transform.IsChildOf(Authority.CollisionRoot.transform);
    public TerrainDigResult Dig(Vector2 world,float radius,TerrainDigAuthority authority,int stage,IReadOnlyCollection<Vector2Int> authorized)
    {
        if(authority==TerrainDigAuthority.ResidentialProgression)
        {
            if(stage!=Authority.Session.Established+1||stage<1||stage>Catalog.TotalCapacity||authorized==null)return new TerrainDigResult(0,0,0,TerrainDigFailureReason.AuthorizationRejected);
            var expected=new HashSet<Vector2Int>(GetResidentialSlotFootprint(stage));
            if(authorized.Any(c=>!expected.Contains(c)))return new TerrainDigResult(0,0,0,TerrainDigFailureReason.AuthorizationRejected);
        }
        var center=Authority.WorldToCell(world);int range=Mathf.CeilToInt(radius/CellSize)+1,checkedCells=0;var ordinary=new List<DualTile>();var residential=new List<DualTile>();
        for(int y=center.y-range;y<=center.y+range;y++)for(int x=center.x-range;x<=center.x+range;x++)
        {
            var p=new Vector2Int(x,y);if((CellToWorld(p)-world).sqrMagnitude>radius*radius)continue;checkedCells++;
            var kind=Authority.Session.Definition.Category(x,y);
            if(kind==CampCell.Expansion&&(authority==TerrainDigAuthority.Player||authority==TerrainDigAuthority.Buddy))ordinary.Add(new DualTile(x,y));
            if(kind==CampCell.Residential&&(authority==TerrainDigAuthority.Player||authority==TerrainDigAuthority.ResidentialProgression&&authorized!=null&&authorized.Contains(p)))residential.Add(new DualTile(x,y));
        }
        ordinary.AddRange(residential);int removed=Authority.Session.DigIsolatedResidence(ordinary,authority,residential);
        return new TerrainDigResult(checkedCells,ordinary.Count,removed,removed>0?TerrainDigFailureReason.None:TerrainDigFailureReason.NoDirtRemoved);
    }
}
