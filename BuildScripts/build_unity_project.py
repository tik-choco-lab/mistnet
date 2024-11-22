import subprocess
import sys
import configparser

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

if __name__ == "__main__":
    config_path = "build_config.ini"
    config = configparser.ConfigParser()
    config.read(config_path)
    
    unity_path = config.get('BuildSettings', 'unity_path', fallback="C:/Program Files/Unity/Hub/Editor/6000.0.25f1/Editor/Unity.exe")
    project_path = config.get('BuildSettings', 'project_path', fallback="")
    method_name = config.get('BuildSettings', 'method_name', fallback="BuildScript.Build")
    build_target = config.get('BuildSettings', 'build_target', fallback="StandaloneWindows64")
    log_file = config.get('BuildSettings', 'log_file', fallback="")
    build_directory = config.get('BuildSettings', 'build_directory', fallback="")
    scenes = config.get('BuildSettings', 'scenes', fallback=None)
    if scenes:
        scenes = scenes.split(',')
    development = config.getboolean('BuildSettings', 'development', fallback=False)

    build_unity_project(unity_path, project_path, method_name, build_target, log_file, build_directory, scenes, development)
