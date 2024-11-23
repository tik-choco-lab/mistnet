import os
import zipfile

def zip_directory(source_dir, output_zip_file):
    with zipfile.ZipFile(output_zip_file, 'w', zipfile.ZIP_DEFLATED) as zipf:
        for root, dirs, files in os.walk(source_dir):
            for file in files:
                file_path = os.path.join(root, file)
                arcname = os.path.relpath(file_path, source_dir)
                zipf.write(file_path, arcname)
                print(f"Added: {arcname}")

    print(f"Directory '{source_dir}' has been zipped to '{output_zip_file}'.")

if __name__ == "__main__":    
    source_directory = "path/to/source/directory"
    output_zip = "output.zip" 
    zip_directory(source_directory, output_zip)
