import io
from concurrent import futures
import grpc
import numpy as np
from PIL import Image, ImageOps
import tensorflow as tf
import ocular_detector_pb2
import ocular_detector_pb2_grpc

MODEL_PATH = "models/keras_model.h5"
model = tf.keras.models.load_model(MODEL_PATH, compile=False)

class OcularClassifierServicer(ocular_detector_pb2_grpc.OcularClassifierServicer):
    def ClasificarEstadoOcular(self, request, context):
        image = Image.open(io.BytesIO(request.imagen_bytes)).convert("RGB")
        size = (224, 224)
        image = ImageOps.fit(image, size, Image.Resampling.LANCZOS)
        image_array = np.asarray(image)
        normalized_image_array = (image_array.astype(np.float32) / 127.5) - 1.0
        data = np.ndarray(shape=(1, 224, 224, 3), dtype=np.float32)
        data[0] = normalized_image_array

        prediction = model.predict(data, verbose=0)
        index = int(np.argmax(prediction))
        confidence_score = float(prediction[0][index])

        ojos_cerrados = index == 1
        estado = "CERRADO" if ojos_cerrados else "ABIERTO"

        return ocular_detector_pb2.PrediccionOcularReply(
            estado_ocular=estado,
            confianza=confidence_score,
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