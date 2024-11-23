import re
import yaml
import os

def load_config(config_path):    
    try:
        with open(config_path, "r", encoding="utf-8") as file:
            config = yaml.safe_load(file)
        return config
    except FileNotFoundError:
        print(f"Error: Config file '{config_path}' not found.")
        return None
    except yaml.YAMLError as e:
        print(f"Error parsing YAML file: {e}")
        return None

def extract_unity_settings(project_path):    
    settings_path = f"{project_path}/ProjectSettings/ProjectSettings.asset"
    product_name = None
    application_version = None

    try:
        with open(settings_path, "r", encoding="utf-8") as file:
            for line in file:
                # Product Name
                product_name_match = re.match(r"^\s*productName:\s*(.+)$", line)
                if product_name_match:
                    product_name = product_name_match.group(1).strip()
                
                # Application Version
                version_match = re.match(r"^\s*bundleVersion:\s*(.+)$", line)
                if version_match:
                    application_version = version_match.group(1).strip()

        return product_name, application_version

    except FileNotFoundError:
        print(f"Error: File '{settings_path}' not found.")
        return None, None
    except Exception as e:
        print(f"An error occurred: {e}")
        return None, None

def main():
    # Load config
    config_path = f"{os.path.dirname(__file__)}/config.yml"
    config = load_config(config_path)
    if not config or "project_path" not in config:
        print("Error: 'project_path' is not specified in the config file.")
        return

    # Extract Unity settings
    project_path = config["project_path"]
    product_name, application_version = extract_unity_settings(project_path)

    # Output results
    if product_name or application_version:
        print(f"Product Name: {product_name}")
        print(f"Application Version: {application_version}")
    else:
        print("No data found or an error occurred.")
        
    return product_name, application_version

if __name__ == "__main__":
    main()
