from build_unity_project import main as build_unity_project
from zip_directory import zip_directory
from ftps_file_transfer import ftps_file_transfer
from get_unity_project_settings import main as get_unity_project_settings
import yaml
import os

product_name, application_version = get_unity_project_settings()

f = open(f"{os.path.dirname(__file__)}/config.yml", "r")
config = yaml.safe_load(f)
f.close()

release = "debug" if config["development"] else "release"
build_directory = config["build_directory"]
build_target = config["build_target"]
build_path = f"{build_directory}{release}/{build_target}"
zip_path = f"{build_path}.zip"
ftp_path = f"/data/{product_name}/{application_version}/{release}/{build_target}.zip"

build_unity_project()

zip_directory(build_path, zip_path)

ftps_file_transfer(zip_path, ftp_path)

print(f"URL: {config["server_url"]}{product_name}/{application_version}/{release}/{build_target}.zip")