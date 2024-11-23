import os
from ftplib import FTP_TLS
from dotenv import load_dotenv
load_dotenv(os.path.join(os.path.dirname(__file__), ".env"))

def ensure_directory_exists(ftps, remote_directory):
    # リモートディレクトリが存在するかを確認し、存在しない場合は作成する
    directories = remote_directory.split("/")
    path = ""
    for directory in directories:
        if directory:  # 空文字列は無視
            path += f"/{directory}"
            try:
                ftps.cwd(path)
            except Exception:
                # ディレクトリが存在しない場合は作成
                ftps.mkd(path)
                ftps.cwd(path)

def ftps_file_transfer(local_file_path, remote_file_path):
    try:
        server = os.getenv('SERVER')
        username = os.getenv('USERNAME')
        password = os.getenv('PASSWORD')

        if not server or not username or not password:
            raise ValueError("FTP credentials are not set in the .env file.")
        
        # FTPS（FTP over TLS）でサーバーに接続
        ftps = FTP_TLS(server)
        ftps.login(user=username, passwd=password)
        ftps.prot_p()  # データチャネルの暗号化を有効化
        print(f"Connected securely to FTPS server: {server}")

        # リモートディレクトリが存在しない場合は作成
        remote_directory = os.path.dirname(remote_file_path)
        if remote_directory:
            ensure_directory_exists(ftps, remote_directory)

        with open(local_file_path, 'rb') as file:
            ftps.storbinary(f"STOR {remote_file_path}", file)
        print(f"File uploaded successfully: {local_file_path} -> {remote_file_path}")

        ftps.quit()
        print("FTPS connection closed.")
    except Exception as e:
        print(f"An error occurred: {e}")

if __name__ == "__main__":
    local_file = "local_file.txt"        
    remote_file = "uploads/remote_file.txt" 
    ftps_file_transfer(local_file, remote_file)
