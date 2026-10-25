"""Generate frozen numeric comparisons from the supplied Python kit, without editing it."""
import csv, hashlib, importlib.util, json, pathlib, shutil, sys

kit = pathlib.Path(sys.argv[1])
script_directory = pathlib.Path(__file__).parent
destination = script_directory if script_directory.name == 'Reference~' else script_directory / 'Tests' / 'Reference~'
destination.mkdir(parents=True, exist_ok=True)
sys.path.insert(0, str(kit))
import locked_geometry as G
occupancy = json.loads((kit / 'cave_occupancy_96x62.json').read_text())['rows']
profile = json.loads((kit / 'grid_projection_exact.json').read_text())
projection = G.Projection.from_sizes(profile['column_widths_px'], profile['row_heights_px'])
(destination / 'occupancy.txt').write_text('\n'.join(''.join(str(int(v)) for v in row) for row in occupancy)+'\n', encoding='utf-8')
for name in ('grid_projection_exact.json', 'locked_geometry_spec.json', 'locked_geometry.py'):
    shutil.copyfile(kit / name, destination / name)
segments = G.all_segments(occupancy)
with (destination / 'curve_golden.csv').open('w', newline='', encoding='utf-8') as stream:
    writer = csv.writer(stream)
    writer.writerow(['template','sample','x','y'])
    for template_index, name in enumerate(G.TEMPLATES):
        for sample_index, point in enumerate(G.template_points(name)):
            writer.writerow([template_index,sample_index,*[format(v,'.17g') for v in point]])
with (destination / 'wall_golden.csv').open('w', newline='', encoding='utf-8') as stream:
    writer = csv.writer(stream)
    writer.writerow(['x','y','nx','ny','top_x','top_y','bottom_x','bottom_y','lip_x','lip_y'])
    for segment in segments[::23]:
        geometry = G.wall_geometry(segment, occupancy, projection)
        for i in (0, 5, 10):
            writer.writerow([format(v,'.17g') for v in (*segment.points[i], *geometry['normals'][i], *geometry['top'][i], *geometry['bottom'][i], *geometry['lip'][i])])
print('Golden rows:', len(segments[::23])*3)
print('Python source SHA256:', hashlib.sha256((kit/'locked_geometry.py').read_bytes()).hexdigest())
