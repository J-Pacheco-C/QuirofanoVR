# Simulador de Quirofano (Unity)

Simulador 3D/VR de quirofano para practicar el uso de equipo biomedico sin comprar equipo real.
Proyecto de Ingenieria Biomedica, CUCEI - UdeG.

## Modulos
- Maquina de anestesia (Mindray WATO EX-20 Vet): rutina guiada de chequeo.
- Desfibrilador (en desarrollo).
- Pruebas de seguridad electrica, IEC 60601-1 (en desarrollo).

## Requisitos
- Unity 6.5 (6000.5.9f1), Universal Render Pipeline.

## Como abrirlo
1. Clona el repositorio.
2. En Unity Hub: Add > selecciona la carpeta del proyecto (la primera vez tarda en importar).
3. Abre `Assets/Scenes/SampleScene` y dale Play.

## Controles
- Moverse: WASD + mouse.
- Interactuar: **E** (mantener E en los pasos que lo piden).
- Reiniciar la rutina: **R**.

## Estructura de la rutina
Los pasos estan en la lista `Pasos` del componente `SequenceManager` (pieza, accion, texto, sprite, movimiento, espera).
Para otro equipo basta con cambiar esa lista.

## Nota sobre el asset
El quirofano 3D es un asset de terceros. Los `.unitypackage` originales no se suben (pesan mas de 100 MB y tienen licencia propia).
