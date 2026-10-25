#!/usr/bin/env python3
"""Locked reusable cave geometry for the Spore Gobbo Unity prototype.

Gameplay is an upright binary square grid. Contours are canonical unit-square
Marching Squares pieces. Display projection and wall depth are derived layers.
No cave-specific geometry is allowed in this module.
"""

from __future__ import annotations

from dataclasses import dataclass
import bisect
import math

KAPPA = 0.5522847498307936
RADIUS = 0.5
K = KAPPA * RADIUS

CORNERS = {"TL": (0.0,0.0), "TR": (1.0,0.0), "BR": (1.0,1.0), "BL": (0.0,1.0)}
BITS = {"TL":1,"TR":2,"BR":4,"BL":8}
ANCHORS = {"T":(.5,0.0),"R":(1.0,.5),"B":(.5,1.0),"L":(0.0,.5)}

# Every endpoint and control point is canonical. H/V are represented as cubic
# lines so the evaluation/tessellation path is identical for every template.
TEMPLATES = {
    "arc_TL": {"p0":ANCHORS["T"],"c1":(.5,K),"c2":(K,.5),"p1":ANCHORS["L"]},
    "arc_TR": {"p0":ANCHORS["T"],"c1":(.5,K),"c2":(1-K,.5),"p1":ANCHORS["R"]},
    "arc_BR": {"p0":ANCHORS["R"],"c1":(1-K,.5),"c2":(.5,1-K),"p1":ANCHORS["B"]},
    "arc_BL": {"p0":ANCHORS["B"],"c1":(.5,1-K),"c2":(K,.5),"p1":ANCHORS["L"]},
    "horizontal": {"p0":ANCHORS["L"],"c1":(1/3,.5),"c2":(2/3,.5),"p1":ANCHORS["R"]},
    "vertical": {"p0":ANCHORS["T"],"c1":(.5,1/3),"c2":(.5,2/3),"p1":ANCHORS["B"]},
}

# Default saddle policy connects SOLID diagonally, preventing a false open
# diagonal passage. The alternative tables are explicit and never selected for
# visual convenience.
CASES = {
    0:[],1:["arc_TL"],2:["arc_TR"],3:["horizontal"],
    4:["arc_BR"],5:["arc_TR","arc_BL"],6:["vertical"],7:["arc_BL"],
    8:["arc_BL"],9:["vertical"],10:["arc_TL","arc_BR"],11:["arc_BR"],
    12:["horizontal"],13:["arc_TR"],14:["arc_TL"],15:[],
}
SADDLE_ALTERNATIVES = {
    5:{"connect_solid":["arc_TR","arc_BL"],"connect_open":["arc_TL","arc_BR"]},
    10:{"connect_solid":["arc_TL","arc_BR"],"connect_open":["arc_TR","arc_BL"]},
}

# Unique topological/geometric families. The remaining cases are rotations,
# reflections, complements, or paired uses of these primitives.
UNIQUE_FAMILIES = {
    "uniform":{"cases":[0,15],"geometry":"no contour; occupancy side differs"},
    "corner_arc":{"cases":[1,2,4,8,7,11,13,14],"geometry":"one quarter-circle; complements reverse solid/open side"},
    "straight":{"cases":[3,6,9,12],"geometry":"one mid-edge straight; rotations/complements"},
    "saddle":{"cases":[5,10],"geometry":"two quarter-circles; connect-solid and connect-open alternatives"},
}

ROTATION_RULES = {
    "rotate90_cw":"(x,y)->(1-y,x); bits TL->TR->BR->BL->TL; anchors T->R->B->L->T",
    "reflect_x":"(x,y)->(1-x,y); bits TL<->TR, BL<->BR; anchors L<->R",
    "reflect_y":"(x,y)->(x,1-y); bits TL<->BL, TR<->BR; anchors T<->B",
    "complement":"case->15-case; identical contour coordinates with solid/open interpretation reversed",
}

TESS_SAMPLES = 10
WALL_HEIGHT_CELLS = 2.15
LIP_WIDTH_CELLS = 0.22
ORIENTATION_FILTER_RADIUS = 2
ORIENTATION_SIGMA = 1.15
WALL_DIRTY_TILE_RADIUS = 4


def cubic_point(t,p0,c1,c2,p1):
    u=1-t
    return (u**3*p0[0]+3*u*u*t*c1[0]+3*u*t*t*c2[0]+t**3*p1[0],
            u**3*p0[1]+3*u*u*t*c1[1]+3*u*t*t*c2[1]+t**3*p1[1])


def template_points(name,samples=TESS_SAMPLES):
    g=TEMPLATES[name]
    return [cubic_point(i/samples,g["p0"],g["c1"],g["c2"],g["p1"]) for i in range(samples+1)]


def classify(tl,tr,br,bl):return (1 if tl else 0)|(2 if tr else 0)|(4 if br else 0)|(8 if bl else 0)


def case_at(occ,x,y):return classify(occ[y][x],occ[y][x+1],occ[y+1][x+1],occ[y+1][x])


def case_grid(occ):
    return [[case_at(occ,x,y) for x in range(len(occ[0])-1)] for y in range(len(occ)-1)]


def segments_for_tile(case_id,x,y,policy="connect_solid"):
    names=(SADDLE_ALTERNATIVES[case_id][policy] if case_id in (5,10) else CASES[case_id])
    return [ContourSegment(case_id,x,y,name,tuple((x+.5+p[0],y+.5+p[1]) for p in template_points(name))) for name in names]


@dataclass(frozen=True)
class ContourSegment:
    case_id:int
    tile_x:int
    tile_y:int
    template:str
    points:tuple[tuple[float,float],...]

    @property
    def key(self):return self.tile_x,self.tile_y,self.template


def all_segments(occ,cases=None):
    cases=cases or case_grid(occ);out=[]
    for y,row in enumerate(cases):
        for x,cid in enumerate(row):out.extend(segments_for_tile(cid,x,y))
    return out


def sample_cell(occ,x,y):
    y=max(0,min(len(occ)-1,y));x=max(0,min(len(occ[0])-1,x));return 1.0 if occ[y][x] else 0.0


def field(occ,x,y):
    sx=x-.5;sy=y-.5;ix=math.floor(sx);iy=math.floor(sy);tx=sx-ix;ty=sy-iy
    a=sample_cell(occ,ix,iy);b=sample_cell(occ,ix+1,iy);c=sample_cell(occ,ix+1,iy+1);d=sample_cell(occ,ix,iy+1)
    return a*(1-tx)*(1-ty)+b*tx*(1-ty)+c*tx*ty+d*(1-tx)*ty


def smoothed_solid_normal(occ,x,y):
    """Deterministic filtered gradient pointing from contour into solid.

    The fixed 5x5 Gaussian sample neighborhood is the one additional wall-
    presentation rule. It removes per-cell height flicker on diagonal runs while
    keeping a finite, documented dirty region for incremental updates.
    """
    eps=.035;gx=gy=ws=0.0
    for oy in range(-ORIENTATION_FILTER_RADIUS,ORIENTATION_FILTER_RADIUS+1):
        for ox in range(-ORIENTATION_FILTER_RADIUS,ORIENTATION_FILTER_RADIUS+1):
            w=math.exp(-(ox*ox+oy*oy)/(2*ORIENTATION_SIGMA*ORIENTATION_SIGMA));ws+=w
            gx+=w*(field(occ,x+ox+eps,y+oy)-field(occ,x+ox-eps,y+oy))/(2*eps)
            gy+=w*(field(occ,x+ox,y+oy+eps)-field(occ,x+ox,y+oy-eps))/(2*eps)
    gx/=ws;gy/=ws;n=math.hypot(gx,gy)
    return (gx/n,gy/n) if n>1e-8 else (0.0,-1.0)


def smoothstep(a,b,v):
    if a==b:return 0.0 if v<a else 1.0
    t=max(0.0,min(1.0,(v-a)/(b-a)))
    return t*t*(3-2*t)


@dataclass(frozen=True)
class Projection:
    widths:tuple[float,...]
    heights:tuple[float,...]
    x_edges:tuple[float,...]
    y_edges:tuple[float,...]

    @staticmethod
    def from_sizes(widths,heights):
        xe=[0.0];ye=[0.0]
        for v in widths:xe.append(xe[-1]+float(v))
        for v in heights:ye.append(ye[-1]+float(v))
        return Projection(tuple(widths),tuple(heights),tuple(xe),tuple(ye))

    @staticmethod
    def _axis(v,edges,sizes):
        if v<=0:return edges[0]+v*sizes[0]
        if v>=len(sizes):return edges[-1]+(v-len(sizes))*sizes[-1]
        i=min(len(sizes)-1,int(math.floor(v)))
        return edges[i]+(v-i)*sizes[i]

    def point(self,p):return self._axis(p[0],self.x_edges,self.widths),self._axis(p[1],self.y_edges,self.heights)
    def dx(self,x):return self.widths[max(0,min(len(self.widths)-1,int(x)))]
    def dy(self,y):return self.heights[max(0,min(len(self.heights)-1,int(y)))]


def wall_geometry(segment,occ,projection,height_cells=WALL_HEIGHT_CELLS):
    """Return one attached strip and one foreground lip for a contour segment.

    Shared contour endpoints query the same global occupancy and projection, so
    neighboring tiles produce bit-identical top, bottom, and lip vertices.
    """
    top=[];bottom=[];lip=[];normals=[];back_values=[];lip_values=[]
    for p in segment.points:
        sx,sy=projection.point(p);nx,ny=smoothed_solid_normal(occ,*p)
        back=smoothstep(.08,.72,-ny);front=smoothstep(.12,.72,ny)
        h=projection.dy(p[1])*height_cells*back
        # Screen-space direction of the logical open normal.
        vx=-nx*projection.dx(p[0]);vy=-ny*projection.dy(p[1]);vn=math.hypot(vx,vy) or 1
        lw=projection.dy(p[1])*LIP_WIDTH_CELLS*front
        top.append((sx,sy));bottom.append((sx,sy+h));lip.append((sx+vx/vn*lw,sy+vy/vn*lw))
        normals.append((nx,ny));back_values.append(back);lip_values.append(front)
    face_quads=[];lip_quads=[]
    for i in range(len(top)-1):
        if max(back_values[i],back_values[i+1])>1e-4:
            face_quads.append((top[i],top[i+1],bottom[i+1],bottom[i]))
        if max(lip_values[i],lip_values[i+1])>1e-4:
            lip_quads.append((top[i],top[i+1],lip[i+1],lip[i]))
    return {"top":tuple(top),"bottom":tuple(bottom),"lip":tuple(lip),"face_quads":tuple(face_quads),"lip_quads":tuple(lip_quads),"normals":tuple(normals)}


def dirty_contour_tiles(cell_x,cell_y,cols,rows):
    return {(x,y) for x,y in ((cell_x-1,cell_y-1),(cell_x,cell_y-1),(cell_x-1,cell_y),(cell_x,cell_y)) if 0<=x<cols-1 and 0<=y<rows-1}


def dirty_wall_tiles(cell_x,cell_y,cols,rows):
    r=WALL_DIRTY_TILE_RADIUS
    return {(x,y) for y in range(max(0,cell_y-r),min(rows-1,cell_y+r)) for x in range(max(0,cell_x-r),min(cols-1,cell_x+r))}


def geometry_signature(geom,precision=7):
    vals=[]
    for name in ("top","bottom","lip"):
        vals.extend((round(x,precision),round(y,precision)) for x,y in geom[name])
    return tuple(vals)
