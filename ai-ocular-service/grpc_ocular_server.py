import os
os.environ["TF_USE_LEGACY_KERAS"] = "1"
import io
from concurrent import futures
import grpc
import numpy as np
from PIL import Image, ImageOps
import tensorflow as tf
import tf_keras as keras
import ocular_detector_pb2
import ocular_detector_pb2_grpc

MODEL_PATH = "models/keras_model.h5"
LABELS_PATH = "models/labels.txt"
UMBRAL_CONFIANZA_CERRADO = 0.78

class CompatibleDepthwiseConv2D(keras.layers.DepthwiseConv2D):
    def __init__(self, **kwargs):
        kwargs.pop("groups", None)
        super().__init__(**kwargs)

model = keras.models.load_model(
    MODEL_PATH,
    custom_objects={"DepthwiseConv2D": CompatibleDepthwiseConv2D},
    compile=False
)

with open(LABELS_PATH, "r", encoding="utf-8") as f:
    class_names = [line.strip() for line in f.readlines() if line.strip()]

indice_clase_cerrado = 1
for idx, nombre in enumerate(class_names):
    etiqueta = nombre.upper()
    if "CERRAD" in etiqueta or "CLOSE" in etiqueta or "SOMNOL" in etiqueta or "DORMID" in etiqueta:
        indice_clase_cerrado = idx
        break

print(f"Etiquetas cargadas: {class_names} -> Indice para CERRADO: {indice_clase_cerrado}")

class OcularClassifierServicer(ocular_detector_pb2_grpc.OcularClassifierServicer):
    def ClasificarEstadoOcular(self, request, context):
        image = Image.open(io.BytesIO(request.imagen_bytes)).convert("RGB")
        image = ImageOps.autocontrast(image, cutoff=1)
        size = (224, 224)
        image = ImageOps.fit(image, size, Image.Resampling.LANCZOS)
        image_array = np.asarray(image)
        normalized_image_array = (image_array.astype(np.float32) / 127.5) - 1.0
        data = np.ndarray(shape=(1, 224, 224, 3), dtype=np.float32)
        data[0] = normalized_image_array

        prediction = model.predict(data, verbose=0)[0]
        prob_cerrado = float(prediction[indice_clase_cerrado])
        prob_abierto = 1.0 - prob_cerrado

        ojos_cerrados = prob_cerrado >= UMBRAL_CONFIANZA_CERRADO
        estado = "CERRADO" if ojos_cerrados else "ABIERTO"
        confianza_final = prob_cerrado if ojos_cerrados else prob_abierto

        print(f"Prob Abierto: {prob_abierto:.2f} | Prob Cerrado: {prob_cerrado:.2f} -> Estado: {estado}")

        return ocular_detector_pb2.PrediccionOcularReply(
            estado_ocular=estado,
            confianza=confianza_final,
            ojos_cerrados=ojos_cerrados
        )

def serve():
    server = grpc.server(futures.ThreadPoolExecutor(max_workers=10))
    ocular_detector_pb2_grpc.add_OcularClassifierServicer_to_server(
        OcularClassifierServicer(), server
    )
    server.add_insecure_port("[::]:50051")
    server.start()
    print("Servidor gRPC con keras_model.h5 activo en puerto 50051")
    server.wait_for_termination()

if __name__ == "__main__":
    serve()