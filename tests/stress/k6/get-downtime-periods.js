// k6 run get-downtime-periods.js

import http from 'k6/http';

export const options = {
    discardResponseBodies: false,
	
	scenarios: {
        s01_coldStart: {
            executor: 'per-vu-iterations',
            startTime: '0s',
            maxDuration: '1s',
            gracefulStop: '1s',
            vus: 1,
            iterations: 1
        },

        s02_tenReqsEveryFiveSecsDuringOneMin:{
            executor: 'constant-arrival-rate',
            startTime: '1s',
            rate: 10,
            timeUnit: '5s',
            duration: '60s',
            preAllocatedVUs: 50
        },

        s03_fiftyReqsEveryFiveSecsDuringTwoMins:{
            executor: 'constant-arrival-rate',
            startTime: '60s',
            rate: 50,
            timeUnit: '5s',
            duration: '120s',
            preAllocatedVUs: 50
        },

        s04_oneHundredReqsEveryFiveSecsDuringTwoMins:{
            executor: 'constant-arrival-rate',
            startTime: '180s',
            rate: 100,
            timeUnit: '5s',
            duration: '120s',
            preAllocatedVUs: 100
        }
    }
};

export default function () {
    const serviceUrl = "http://localhost:81";
    http.get(`${serviceUrl}/report/downtimePeriods`);
}