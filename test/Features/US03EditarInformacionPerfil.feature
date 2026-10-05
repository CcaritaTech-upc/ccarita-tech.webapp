Feature: US03 Edición de Información del Perfil
  Como usuario
  quiero poder editar alguna parte de mi información, como mi email, número de teléfono o dirección
  para mantener mis datos actualizados.

  @US03
  Scenario Outline: Edición exitosa de información de contacto
    Given el usuario con rol <rol> y alias <usuario> está editando su perfil
    When modifica el teléfono a "<nuevo_telefono>" y la dirección a "<nueva_direccion>"
    And guarda los cambios
    Then el sistema actualizará los datos satisfactoriamente
    And mostrará el mensaje de confirmación "<mensaje_exito>"
    Examples:
      | rol | usuario | nuevo_telefono | nueva_direccion | mensaje_exito |
      | Builder | axel | 999888777 | Av. Primavera 123 | Perfil actualizado correctamente |
      | Owner | mateo | 988777666 | Calle Los Fresnos 456 | Perfil actualizado correctamente |

  @US03
  Scenario Outline: Validación de formato incorrecto en datos de contacto
    Given el usuario con rol <rol> y alias <usuario> está editando su perfil
    When ingresa un teléfono inválido "<telefono_invalido>"
    And intenta guardar el perfil
    Then el sistema bloqueará la actualización
    And mostrará la validación de error "<mensaje_error>"
    Examples:
      | rol | usuario | telefono_invalido | mensaje_error |
      | Builder | axel | 123 | Número de teléfono inválido (debe contener entre 7 y 15 dígitos). |
      | Owner | mateo | 123 | Número de teléfono inválido (debe contener entre 7 y 15 dígitos). |
