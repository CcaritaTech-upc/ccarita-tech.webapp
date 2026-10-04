Feature: US05 Agregar un nuevo proyecto
  Como arquitecto
  quiero agregar un nuevo proyecto
  para poder registrar nuevos desarrollos inmobiliarios e integrar funcionalidades inteligentes.

  Scenario Outline: Registro exitoso de un proyecto con datos completos
    Given que el arquitecto <usuario> se encuentra en el formulario de "Nuevo Proyecto"
    And hace clic en "Crear Proyecto"
    When ingresa el nombre <nombre_proyecto>, ubicación <ubicacion> y total de unidades <unidades>
    Then el sistema creará el proyecto en estado registrado con código 201 Created
    And mostrará el mensaje <mensaje_exito>
    Examples:
      | usuario | nombre_proyecto | ubicacion | unidades | mensaje_exito |
      | mateo@iobuild.pe | Edificio Panorama | Surco, Lima | 32 | Proyecto creado exitosamente |
      | jhosep@iobuild.pe | Mirador del Valle | Cayma, Arequipa | 20 | Proyecto creado exitosamente |

  Scenario Outline: Rechazo de registro por datos obligatorios faltantes
    Given que el arquitecto <usuario> abre el formulario de creación de proyecto
    And hace clic en "Crear Proyecto"
    When omite ingresar el campo obligatorio <campo_faltante>
    Then el sistema denegará la creación con código 422 UnprocessableEntity
    And resaltará el campo con el mensaje <mensaje_error>
    Examples:
      | usuario | campo_faltante | mensaje_error |
      | mateo@iobuild.pe | nombre | El nombre del proyecto es requerido |
      | mateo@iobuild.pe | unidades | El número de unidades debe ser mayor a 0 |
