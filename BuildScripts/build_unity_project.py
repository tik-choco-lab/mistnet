import subprocess
import sys
import yaml
import os

def load_build_config(config_path):
    try:
        with open(config_path, "r", encoding="utf-8") as file:
            config = yaml.safe_load(file)
        return config
    except FileNotFoundError:
        print(f"Error: Configuration file '{config_path}' not found.")
        sys.exit(1)
    except yaml.YAMLError as e:
        print(f"Error parsing YAML file: {e}")
        sys.exit(1)

def parse_scenes(scenes):
    return scenes.split(',') if scenes else None

def build_unity_project(unity_path, project_path, method_name, build_target, log_file=None, build_directory=None, scenes=None, development=False):
    cmd = [
        unity_path,
        '-batchmode',
        '-projectPath', project_path,
        '-executeMethod', method_name,
        '-buildTarget', build_target,
        '-quit'
    ]

    if log_file:
        cmd.extend(['-logFile', log_file])

    if build_directory:
        cmd.extend(['-buildDirectory', build_directory])

    if scenes:
        scenes_arg = ','.join(scenes)
        cmd.extend(['-scenes', scenes_arg])

    if development:
        cmd.extend(['-buildOptions', 'Development'])

    try:
        subprocess.run(cmd, check=True)
        print(f"Build completed successfully for target: {build_target}")
    except subprocess.CalledProcessError as e:
        print(f"Error during build: {e}")
        sys.exit(1)
        
        
def main():    
    config_path = f"{os.path.dirname(__file__)}/config.yml"
    
    build_settings = load_build_config(config_path)
    print(build_settings)
    
    unity_path = build_settings.get('unity_path', "C:/Program Files/Unity/Hub/Editor/6000.0.25f1/Editor/Unity.exe")
    project_path = build_settings.get('project_path', "")
    method_name = build_settings.get('method_name', "BuildScript.Build")
    build_target = build_settings.get('build_target', "StandaloneWindows64")
    log_file = build_settings.get('log_file', None)
    build_directory = build_settings.get('build_directory', None)
    scenes = parse_scenes(build_settings.get('scenes', None))
    development = build_settings.get('development', False)

    # Run the build
    build_unity_project(unity_path, project_path, method_name, build_target, log_file, build_directory, scenes, development)

if __name__ == "__main__":
    main()