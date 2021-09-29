ARG FROM_IMG=huggingface/transformers-pytorch-gpu
FROM $FROM_IMG
WORKDIR /app
COPY . .
ENTRYPOINT ["python3", "/app/text-classification-server.py"]