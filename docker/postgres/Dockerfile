FROM postgres:17

COPY init/01-roles.sh /docker-entrypoint-initdb.d/01-roles.sh
RUN chmod +x /docker-entrypoint-initdb.d/01-roles.sh